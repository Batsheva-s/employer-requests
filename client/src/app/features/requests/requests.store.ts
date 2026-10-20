import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, merge, of, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/http/http-error.util';
import {
  BulkChangeStatusResult,
  DEFAULT_QUERY,
  PagedResult,
  RequestFilters,
  RequestListItem,
  RequestQuery,
  RequestStatus,
  SortDirection,
  SortField,
  filtersOf,
  sameValue,
} from '../../core/models/request.models';
import { RequestsApiService } from '../../core/services/requests-api.service';

type SearchOutcome = { result: PagedResult<RequestListItem> | null; error: string | null };

/**
 * The state of the requests screen, provided by the page component.
 * Components read signals and call methods; only the store talks to the API about the list.
 * The client never filters, sorts or pages: every change of the query asks the server for one page.
 */
@Injectable()
export class RequestsStore {
  private readonly api = inject(RequestsApiService);
  private readonly reload$ = new Subject<void>();

  // ---------- State ----------

  readonly query = signal<RequestQuery>(DEFAULT_QUERY);
  readonly result = signal<PagedResult<RequestListItem> | null>(null);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  /** The requests selected for a bulk change: id → the version the user sees on the screen. */
  readonly selection = signal<ReadonlyMap<number, string>>(new Map());

  // ---------- Derived state ----------

  readonly filters = computed(() => filtersOf(this.query()), { equal: sameValue });
  readonly isEmpty = computed(() => !this.loading() && !this.errorMessage() && this.result()?.totalCount === 0);
  readonly selectedIds = computed(() => new Set(this.selection().keys()));

  constructor() {
    // One search for every change of the query, and for every explicit reload.
    // switchMap cancels the search that is still on its way when a newer one starts: the browser aborts the HTTP
    // call (and the server cancels its database query), so a slow, old answer can never replace a newer one.
    merge(toObservable(this.query), this.reload$.pipe(map(() => this.query())))
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.errorMessage.set(null);
        }),
        switchMap((query) =>
          this.api.search(query).pipe(
            map((result): SearchOutcome => ({ result, error: null })),
            catchError((error: unknown) => of<SearchOutcome>({ result: null, error: describeHttpError(error) })),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(({ result, error }) => {
        this.loading.set(false);
        this.errorMessage.set(error);
        if (result) {
          this.result.set(result);
          this.refreshSelectedVersions(result.items);
        }
      });
  }

  // ---------- Query ----------

  /** New filters start again from the first page, and the old selection no longer applies. */
  setFilters(filters: RequestFilters): void {
    if (sameValue(filters, this.filters())) {
      return; // the same filters again (a letter typed and deleted): keep the page, the selection and the results
    }
    this.query.update((query) => ({ ...query, ...filters, page: 1 }));
    this.clearSelection();
  }

  setSort(sortBy: SortField, sortDirection: SortDirection): void {
    this.query.update((query) => ({ ...query, sortBy, sortDirection, page: 1 }));
  }

  setPage(page: number, pageSize: number): void {
    // A different page size changes what "page 3" means, so it starts again from the first page.
    this.query.update((query) => ({ ...query, pageSize, page: pageSize === query.pageSize ? page : 1 }));
  }

  /** The same query again – after a status change, or after an error. */
  reload(): void {
    this.reload$.next();
  }

  // ---------- Selection and bulk change ----------

  toggleSelection(item: RequestListItem): void {
    this.selection.update((current) => {
      const next = new Map(current);
      if (next.has(item.id)) {
        next.delete(item.id);
      } else {
        next.set(item.id, item.rowVersion);
      }
      return next;
    });
  }

  /** Selects every request on the current page, or clears them if all are selected already. */
  toggleAllOnPage(): void {
    const items = this.result()?.items ?? [];
    const allSelected = items.length > 0 && items.every((item) => this.selection().has(item.id));

    this.selection.update((current) => {
      const next = new Map(current);
      for (const item of items) {
        if (allSelected) {
          next.delete(item.id);
        } else {
          next.set(item.id, item.rowVersion);
        }
      }
      return next;
    });
  }

  clearSelection(): void {
    this.selection.set(new Map());
  }

  /** Changes the status of every selected request. Afterwards the selection is cleared and the list reloaded. */
  changeStatusOfSelected(status: RequestStatus): Observable<BulkChangeStatusResult> {
    const items = [...this.selection()].map(([id, rowVersion]) => ({ id, rowVersion }));

    return this.api.changeStatusBulk(status, items).pipe(
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

    this.selection.update((current) => {
      const next = new Map(current);
      for (const item of items) {
        if (next.has(item.id)) {
          next.set(item.id, item.rowVersion);
        }
      }
      return next;
    });
  }
}
