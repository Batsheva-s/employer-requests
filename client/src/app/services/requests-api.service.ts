import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BulkChangeItem,
  BulkChangeStatusResult,
  PagedResult,
  RequestDetails,
  RequestFilters,
  RequestListItem,
  RequestQuery,
  RequestStatus,
  RequestStatusChange,
  RequestsSummary,
} from '../models/request.models';

/** The HTTP calls of the requests API – one method per endpoint, no state, so one instance serves the whole app. */
@Injectable({ providedIn: 'root' })
export class RequestsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/requests';

  /** One page of requests. Filtering, sorting and paging are all done by the server. */
  search(query: RequestQuery): Observable<PagedResult<RequestListItem>> {
    let params = toFilterParams(query);
    params = params.set('page', query.page);
    params = params.set('pageSize', query.pageSize);
    params = params.set('sortBy', query.sortBy);
    params = params.set('sortDirection', query.sortDirection);

    return this.http.get<PagedResult<RequestListItem>>(this.baseUrl, { params: params });
  }

  /** Counts for the same filters as the list. */
  getSummary(filters: RequestFilters): Observable<RequestsSummary> {
    const params = toFilterParams(filters);
    return this.http.get<RequestsSummary>(`${this.baseUrl}/summary`, { params: params });
  }

  getById(id: number): Observable<RequestDetails> {
    return this.http.get<RequestDetails>(`${this.baseUrl}/${id}`);
  }

  getHistory(id: number): Observable<RequestStatusChange[]> {
    return this.http.get<RequestStatusChange[]>(`${this.baseUrl}/${id}/history`);
  }

  /** Saved only if the request is still in the version the user saw (rowVersion); otherwise the server answers 409. */
  changeStatus(id: number, status: RequestStatus, rowVersion: string): Observable<RequestDetails> {
    const body = { status: status, rowVersion: rowVersion };
    return this.http.patch<RequestDetails>(`${this.baseUrl}/${id}/status`, body);
  }

  /** Every request gets its own outcome; the answer is 200 even if some were not updated. */
  changeStatusBulk(status: RequestStatus, items: BulkChangeItem[]): Observable<BulkChangeStatusResult> {
    const body = { status: status, items: items };
    return this.http.patch<BulkChangeStatusResult>(`${this.baseUrl}/status`, body);
  }
}

/**
 * The filters as query string parameters. Empty filters are left out.
 * HttpParams never changes: set() and append() return a new copy, so the result is kept in params every time.
 */
function toFilterParams(filters: RequestFilters): HttpParams {
  let params = new HttpParams();

  if (filters.search.trim() !== '') {
    params = params.set('search', filters.search.trim());
  }
  if (filters.organizationName.trim() !== '') {
    params = params.set('organizationName', filters.organizationName.trim());
  }
  if (filters.assignedTo.trim() !== '') {
    params = params.set('assignedTo', filters.assignedTo.trim());
  }

  // One parameter per chosen value: ?status=New&status=Waiting
  for (const status of filters.status) {
    params = params.append('status', status);
  }
  for (const priority of filters.priority) {
    params = params.append('priority', priority);
  }

  // The user picks calendar days in their own time zone; the API filters exact moments, from (inclusive) to
  // (exclusive). So "from" is the start of the first day, and "to" is the start of the day after the last one,
  // both converted to UTC by toISOString().
  if (filters.createdFrom !== '') {
    params = params.set('createdFrom', startOfDay(filters.createdFrom, 0).toISOString());
  }
  if (filters.createdTo !== '') {
    params = params.set('createdTo', startOfDay(filters.createdTo, 1).toISOString());
  }

  return params;
}

/** Midnight in the user's time zone at the start of a yyyy-MM-dd day – or of a day some days later. */
function startOfDay(day: string, addDays: number): Date {
  // "2026-03-01" → ["2026", "03", "01"]
  const parts = day.split('-');
  const year = Number(parts[0]);
  const month = Number(parts[1]);
  const date = Number(parts[2]);

  // In JavaScript, months are counted from 0. A day past the end of the month moves on by itself:
  // 31 March + 1 day → 1 April.
  return new Date(year, month - 1, date + addDays);
}
