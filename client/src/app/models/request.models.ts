// The shapes of the API's data – they mirror the DTOs in EmployerRequests.Application/DTOs.

export const REQUEST_STATUSES = ['New', 'InProgress', 'Waiting', 'Completed'] as const;
export type RequestStatus = (typeof REQUEST_STATUSES)[number];

export const REQUEST_PRIORITIES = ['Low', 'Medium', 'High'] as const;
export type RequestPriority = (typeof REQUEST_PRIORITIES)[number];

export type SortField = 'createdAt' | 'updatedAt' | 'title' | 'organizationName' | 'status' | 'priority';
export type SortDirection = 'asc' | 'desc';

/** A row of the list. rowVersion is the version the user is looking at – sent back when changing the status. */
export interface RequestListItem {
  id: number;
  title: string;
  organizationName: string;
  status: RequestStatus;
  priority: RequestPriority;
  assignedTo: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface RequestDetails extends RequestListItem {
  /** The statuses the workflow allows next – the only ones the user is offered. */
  allowedNextStatuses: RequestStatus[];
}

export interface RequestStatusChange {
  id: number;
  previousStatus: RequestStatus;
  newStatus: RequestStatus;
  changedBy: string;
  changedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface RequestsSummary {
  totalCount: number;
  byStatus: { status: RequestStatus; count: number }[];
  byPriority: { priority: RequestPriority; count: number }[];
  topAssignees: { assignedTo: string; openRequests: number }[];
  calculatedAt: string;
}

export type BulkItemOutcome = 'Updated' | 'NotFound' | 'Conflict' | 'InvalidTransition';

export interface BulkChangeStatusResult {
  requested: number;
  succeeded: number;
  failed: number;
  items: { id: number; outcome: BulkItemOutcome; message: string | null; rowVersion: string | null }[];
}

/** The filters as the user enters them. Dates are calendar days (yyyy-MM-dd) in the user's time zone. */
export interface RequestFilters {
  search: string;
  status: RequestStatus[];
  priority: RequestPriority[];
  organizationName: string;
  assignedTo: string;
  createdFrom: string;
  createdTo: string;
}

/** The filters plus paging and sorting – everything the list needs from the server. */
export interface RequestQuery extends RequestFilters {
  page: number;
  pageSize: number;
  sortBy: SortField;
  sortDirection: SortDirection;
}

export const EMPTY_FILTERS: RequestFilters = {
  search: '',
  status: [],
  priority: [],
  organizationName: '',
  assignedTo: '',
  createdFrom: '',
  createdTo: '',
};

export const DEFAULT_QUERY: RequestQuery = {
  ...EMPTY_FILTERS,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

export function filtersOf(query: RequestQuery): RequestFilters {
  const { search, status, priority, organizationName, assignedTo, createdFrom, createdTo } = query;
  return { search, status, priority, organizationName, assignedTo, createdFrom, createdTo };
}

/** Value equality for plain data (filters, queries). */
export function sameValue<T>(a: T, b: T): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}
