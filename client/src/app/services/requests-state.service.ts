import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, of, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../utils/http-errors';
import {
  BulkChangeItem,
  BulkChangeStatusResult,
  DEFAULT_QUERY,
  PagedResult,
  RequestFilters,
  RequestListItem,
  RequestQuery,
  RequestStatus,
  RequestsSummary,
  SortDirection,
  SortField,
  sameFilters,
} from '../models/request.models';
import { RequestsApiService } from './requests-api.service';

/**
 * The state of the requests screen: the query, the current page of results, the summary, loading and errors,
 * and the selection. Provided by the page component (one instance per page). Components read its signals and
 * call its methods; only this service asks the API for the list and for the summary.
 * The client never filters, sorts or pages: every change of the query asks the server for one page.
 */
@Injectable()
export class RequestsStateService {
  private readonly api = inject(RequestsApiService);

  /** Every search goes through this stream (see the constructor). */
  private readonly searches = new Subject<RequestQuery>();
  /** Every summary load goes through this stream (see the constructor). */
  private readonly summaryLoads = new Subject<RequestFilters>();

  // ---------- The list ----------

  readonly query = signal<RequestQuery>(DEFAULT_QUERY);
  readonly result = signal<PagedResult<RequestListItem> | null>(null);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  /** True when the search succeeded and found nothing. */
  readonly isEmpty = computed(() => {
    const result = this.result();
    if (this.loading() || this.errorMessage() !== null || result === null) {
      return false;
    }
    return result.totalCount === 0;
  });

  // ---------- The summary: counts for the same filters as the list ----------

  readonly summary = signal<RequestsSummary | null>(null);
  readonly summaryLoading = signal(false);
  readonly summaryError = signal<string | null>(null);

  // ---------- The selection for a bulk change ----------

  /**
   * The selected requests: id → the version the user sees on the screen.
   * The Map is never changed in place – every change sets a new Map, so the signal knows something changed.
   */
  readonly selection = signal<ReadonlyMap<number, string>>(new Map());

  /** The ids of the selected requests, for the table's checkboxes. */
  readonly selectedIds = computed(() => new Set(this.selection().keys()));

  constructor() {
    // Every search goes through one pipe:
    // - switchMap: when a new search starts while the previous one is still on its way, the previous one is
    //   cancelled – the browser aborts its HTTP call, and the server stops its database query. So an old, slow
    //   answer can never replace a newer one.
    // - takeUntilDestroyed: the pipe closes when the page (which provides this service) is destroyed.
    this.searches
      .pipe(
        switchMap((query) => this.askServerForPage(query)),
        takeUntilDestroyed(),
      )
      .subscribe((page) => this.showPage(page));

    // The summary goes through a pipe of its own, built the same way.
    this.summaryLoads
      .pipe(
        switchMap((filters) => this.askServerForSummary(filters)),
        takeUntilDestroyed(),
      )
      .subscribe((summary) => this.showSummary(summary));

    // The first screen. (Only now: a Subject passes a value only to those already subscribed above.)
    this.search();
    this.loadSummary();
  }

  // ---------- Changing the query ----------

  /** New filters start again from the first page, and the old selection no longer applies. */
  setFilters(filters: RequestFilters): void {
    const query = this.query();
    if (sameFilters(filters, query)) {
      return; // the same filters again (a letter typed and deleted): keep the page, the selection and the results
    }

    this.query.set({
      search: filters.search,
      status: filters.status,
      priority: filters.priority,
      organizationName: filters.organizationName,
      assignedTo: filters.assignedTo,
      createdFrom: filters.createdFrom,
      createdTo: filters.createdTo,
      page: 1,
      pageSize: query.pageSize,
      sortBy: query.sortBy,
      sortDirection: query.sortDirection,
    });
    this.clearSelection();
    this.search();
    this.loadSummary(); // the summary counts the same requests as the list
  }

  /** A new sort starts again from the first page. */
  setSort(sortBy: SortField, sortDirection: SortDirection): void {
    // { ...query } is a copy of the query; the fields written after it replace the copied ones.
    const query = this.query();
    this.query.set({ ...query, sortBy: sortBy, sortDirection: sortDirection, page: 1 });
    this.search();
  }

  setPage(page: number, pageSize: number): void {
    const query = this.query();

    // A different page size changes what "page 3" means, so it starts again from the first page.
    let newPage = page;
    if (pageSize !== query.pageSize) {
      newPage = 1;
    }

    this.query.set({ ...query, page: newPage, pageSize: pageSize });
    this.search();
  }

  /** The same query again – after a status change, or after an error. */
  reload(): void {
    this.search();
    this.loadSummary();
  }

  private search(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.searches.next(this.query());
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.summaryError.set(null);
    // The query holds the filters (with paging and sorting, which the summary ignores).
    this.summaryLoads.next(this.query());
  }

  // ---------- Talking to the server ----------

  /** One page from the server. If the call fails, the error message is set and null comes back instead. */
  private askServerForPage(query: RequestQuery): Observable<PagedResult<RequestListItem> | null> {
    return this.api.search(query).pipe(
      // A failed call ends only itself; the pipe stays open for the next search.
      catchError((error: unknown) => {
        this.errorMessage.set(describeHttpError(error));
        return of(null);
      }),
    );
  }

  private showPage(page: PagedResult<RequestListItem> | null): void {
    this.loading.set(false);
    if (page === null) {
      return; // the call failed, and errorMessage is already set
    }

    this.result.set(page);
    this.refreshSelectedVersions(page.items);
  }

  /** The summary from the server – the same as askServerForPage, for the summary. */
  private askServerForSummary(filters: RequestFilters): Observable<RequestsSummary | null> {
    return this.api.getSummary(filters).pipe(
      catchError((error: unknown) => {
        this.summaryError.set(describeHttpError(error));
        return of(null);
      }),
    );
  }

  private showSummary(summary: RequestsSummary | null): void {
    this.summaryLoading.set(false);
    if (summary === null) {
      return; // the call failed, and summaryError is already set
    }

    this.summary.set(summary);
  }

  // ---------- Selection and bulk change ----------

  toggleSelection(item: RequestListItem): void {
    const next = new Map(this.selection());
    if (next.has(item.id)) {
      next.delete(item.id);
    } else {
      next.set(item.id, item.rowVersion);
    }
    this.selection.set(next);
  }

  /** Selects every request on the current page, or clears them if all are selected already. */
  toggleAllOnPage(): void {
    const result = this.result();
    if (result === null || result.items.length === 0) {
      return;
    }

    const allSelected = result.items.every((item) => this.selection().has(item.id));
    const next = new Map(this.selection());
    for (const item of result.items) {
      if (allSelected) {
        next.delete(item.id);
      } else {
        next.set(item.id, item.rowVersion);
      }
    }
    this.selection.set(next);
  }

  clearSelection(): void {
    this.selection.set(new Map());
  }

  /** Changes the status of every selected request. Afterwards the selection is cleared and the screen reloaded. */
  changeStatusOfSelected(status: RequestStatus): Observable<BulkChangeStatusResult> {
    const items: BulkChangeItem[] = [];
    // Every entry of the Map is a pair: [id, rowVersion].
    for (const [id, rowVersion] of this.selection()) {
      items.push({ id: id, rowVersion: rowVersion });
    }

    return this.api.changeStatusBulk(status, items).pipe(
      // tap runs when the answer arrives, before the caller receives it.
      tap(() => {
        this.clearSelection();
        this.reload();
      }),
    );
  }

  /** A selected request that is on the screen again carries the version the user now sees. */
  private refreshSelectedVersions(items: RequestListItem[]): void {
    if (this.selection().size === 0) {
      return;
    }

    const next = new Map(this.selection());
    for (const item of items) {
      if (next.has(item.id)) {
        next.set(item.id, item.rowVersion);
      }
    }
    this.selection.set(next);
  }
}
