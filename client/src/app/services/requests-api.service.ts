import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
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

/** The HTTP calls of the requests API – one method per endpoint, no state. */
@Injectable({ providedIn: 'root' })
export class RequestsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/requests';

  /** One page of requests. Filtering, sorting and paging are all done by the server. */
  search(query: RequestQuery): Observable<PagedResult<RequestListItem>> {
    const params = toFilterParams(query)
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDirection', query.sortDirection);

    return this.http.get<PagedResult<RequestListItem>>(this.baseUrl, { params });
  }

  /** Counts for the same filters as the list. */
  getSummary(filters: RequestFilters): Observable<RequestsSummary> {
    return this.http.get<RequestsSummary>(`${this.baseUrl}/summary`, { params: toFilterParams(filters) });
  }

  getById(id: number): Observable<RequestDetails> {
    return this.http.get<RequestDetails>(`${this.baseUrl}/${id}`);
  }

  getHistory(id: number): Observable<RequestStatusChange[]> {
    return this.http.get<RequestStatusChange[]>(`${this.baseUrl}/${id}/history`);
  }

  /** Saved only if the request is still in the version the user saw (rowVersion); otherwise the server answers 409. */
  changeStatus(id: number, status: RequestStatus, rowVersion: string): Observable<RequestDetails> {
    return this.http.patch<RequestDetails>(`${this.baseUrl}/${id}/status`, { status, rowVersion });
  }

  /** Every request gets its own outcome; the answer is 200 even if some were not updated. */
  changeStatusBulk(status: RequestStatus, items: { id: number; rowVersion: string }[]): Observable<BulkChangeStatusResult> {
    return this.http.patch<BulkChangeStatusResult>(`${this.baseUrl}/status`, { status, items });
  }
}

/** The filters as query string parameters. Empty filters are left out. */
function toFilterParams(filters: RequestFilters): HttpParams {
  let params = new HttpParams();

  const texts: [string, string][] = [
    ['search', filters.search],
    ['organizationName', filters.organizationName],
    ['assignedTo', filters.assignedTo],
  ];
  for (const [name, value] of texts) {
    if (value.trim()) {
      params = params.set(name, value.trim());
    }
  }

  for (const status of filters.status) {
    params = params.append('status', status);
  }
  for (const priority of filters.priority) {
    params = params.append('priority', priority);
  }

  // The user picks calendar days in their own time zone; the API filters exact moments, from (inclusive) to
  // (exclusive). So "from" is the start of the first day, and "to" is the start of the day after the last one,
  // both converted to UTC by toISOString().
  if (filters.createdFrom) {
    params = params.set('createdFrom', startOfDay(filters.createdFrom).toISOString());
  }
  if (filters.createdTo) {
    params = params.set('createdTo', startOfDay(filters.createdTo, 1).toISOString());
  }

  return params;
}

/** Local midnight of a yyyy-MM-dd day, optionally some days later. */
function startOfDay(day: string, addDays = 0): Date {
  const [year, month, date] = day.split('-').map(Number);
  return new Date(year, month - 1, date + addDays);
}
