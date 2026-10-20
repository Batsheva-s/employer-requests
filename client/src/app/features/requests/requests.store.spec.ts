import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EMPTY_FILTERS, PagedResult, RequestListItem } from '../../core/models/request.models';
import { RequestsStore } from './requests.store';

describe('RequestsStore', () => {
  let store: RequestsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), RequestsStore],
    });
    store = TestBed.inject(RequestsStore);
    http = TestBed.inject(HttpTestingController);
    TestBed.tick(); // lets the store react to its initial query – the first search starts
  });

  afterEach(() => http.verify());

  it('asks the server for the first page and shows it', () => {
    const search = http.expectOne((request) => request.url === '/api/requests');
    expect(search.request.params.get('page')).toBe('1');
    expect(search.request.params.get('pageSize')).toBe('20');
    expect(store.loading()).toBe(true);

    search.flush(page([item(1), item(2)], 2));

    expect(store.loading()).toBe(false);
    expect(store.result()?.items.map((request) => request.id)).toEqual([1, 2]);
  });

  it('cancels the search still on its way when the filters change, and shows only the newest answer', () => {
    const first = http.expectOne((request) => request.url === '/api/requests');

    store.setFilters({ ...EMPTY_FILTERS, search: 'אישור' });
    TestBed.tick();

    const second = http.expectOne((request) => request.params.get('search') === 'אישור');
    expect(first.cancelled).toBe(true);

    second.flush(page([item(7)], 1));
    expect(store.result()?.items.map((request) => request.id)).toEqual([7]);
  });

  it('reports an empty result', () => {
    http.expectOne((request) => request.url === '/api/requests').flush(page([], 0));

    expect(store.isEmpty()).toBe(true);
  });

  it('shows an error message, and a reload asks again', () => {
    http
      .expectOne((request) => request.url === '/api/requests')
      .flush({ title: 'An unexpected error occurred' }, { status: 500, statusText: 'Server Error' });
    expect(store.errorMessage()).toContain('תקלה בשרת');

    store.reload();
    http.expectOne((request) => request.url === '/api/requests').flush(page([item(1)], 1));

    expect(store.errorMessage()).toBeNull();
    expect(store.result()?.totalCount).toBe(1);
  });
});

function page(items: RequestListItem[], totalCount: number): PagedResult<RequestListItem> {
  return { items, page: 1, pageSize: 20, totalCount, totalPages: Math.ceil(totalCount / 20) };
}

function item(id: number): RequestListItem {
  return {
    id,
    title: `פנייה ${id}`,
    organizationName: 'אלון תעשיות בע"מ',
    status: 'New',
    priority: 'Medium',
    assignedTo: null,
    createdAt: '2026-01-01T08:00:00+00:00',
    updatedAt: '2026-01-01T08:00:00+00:00',
    rowVersion: 'AAAAAAAAB9E=',
  };
}
