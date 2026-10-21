// The shapes of the API's data – they mirror the DTOs in EmployerRequests.Application/DTOs.

export type RequestStatus = 'New' | 'InProgress' | 'Waiting' | 'Completed';
export const REQUEST_STATUSES: RequestStatus[] = ['New', 'InProgress', 'Waiting', 'Completed'];

export type RequestPriority = 'Low' | 'Medium' | 'High';
export const REQUEST_PRIORITIES: RequestPriority[] = ['Low', 'Medium', 'High'];

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

/** One request of a bulk change: its id and the version the user saw. */
export interface BulkChangeItem {
  id: number;
  rowVersion: string;
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

/** The first screen: no filters, the newest requests first, 20 per page. */
export const DEFAULT_QUERY: RequestQuery = {
  search: '',
  status: [],
  priority: [],
  organizationName: '',
  assignedTo: '',
  createdFrom: '',
  createdTo: '',
  page: 1,
  pageSize: 20,
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

/** True if both have exactly the same filters. Paging and sorting are not compared. */
export function sameFilters(a: RequestFilters, b: RequestFilters): boolean {
  return (
    a.search === b.search &&
    // Two arrays are never === each other, so their values are compared: ['New', 'Waiting'] → "New,Waiting".
    a.status.join(',') === b.status.join(',') &&
    a.priority.join(',') === b.priority.join(',') &&
    a.organizationName === b.organizationName &&
    a.assignedTo === b.assignedTo &&
    a.createdFrom === b.createdFrom &&
    a.createdTo === b.createdTo
  );
}
