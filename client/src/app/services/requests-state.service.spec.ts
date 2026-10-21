import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PagedResult, RequestFilters, RequestListItem } from '../models/request.models';
import { RequestsStateService } from './requests-state.service';

describe('RequestsStateService', () => {
  let state: RequestsStateService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), RequestsStateService],
    });
    // Creating the service starts the first search and the first summary load.
    state = TestBed.inject(RequestsStateService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // The summary is loaded next to the list – when the service starts, when the filters change and on reload.
    // The tests that do not check the summary leave those calls unanswered.
    http.match((request) => request.url === '/api/requests/summary');
    http.verify(); // no other call was made without the test expecting it
  });

  it('asks the server for the first page and shows it', () => {
    const search = expectSearch();
    expect(search.request.params.get('page')).toBe('1');
    expect(search.request.params.get('pageSize')).toBe('20');
    expect(state.loading()).toBe(true);

    search.flush(page([item(1), item(2)], 2));

    expect(state.loading()).toBe(false);
    expect(state.result()?.items.map((request) => request.id)).toEqual([1, 2]);
  });

  it('when the filters change, cancels the calls still on their way and asks again – for the list and the summary', () => {
    const firstSearch = expectSearch();
    const firstSummary = http.expectOne((request) => request.url === '/api/requests/summary');

    state.setFilters(filters('אישור'));

    const secondSearch = http.expectOne(
      (request) => request.url === '/api/requests' && request.params.get('search') === 'אישור',
    );
    const secondSummary = http.expectOne(
      (request) => request.url === '/api/requests/summary' && request.params.get('search') === 'אישור',
    );
    expect(firstSearch.cancelled).toBe(true);
    expect(firstSummary.cancelled).toBe(true);
    expect(secondSummary.cancelled).toBe(false);

    secondSearch.flush(page([item(7)], 1));
    expect(state.result()?.items.map((request) => request.id)).toEqual([7]);
  });

  it('reports an empty result', () => {
    expectSearch().flush(page([], 0));

    expect(state.isEmpty()).toBe(true);
  });

  it('shows an error message, and a reload asks again', () => {
    expectSearch().flush({ title: 'An unexpected error occurred' }, { status: 500, statusText: 'Server Error' });
    expect(state.errorMessage()).toContain('תקלה בשרת');

    state.reload();
    expectSearch().flush(page([item(1)], 1));

    expect(state.errorMessage()).toBeNull();
    expect(state.result()?.totalCount).toBe(1);
  });

  /** The one search call that is open now. */
  function expectSearch(): TestRequest {
    return http.expectOne((request) => request.url === '/api/requests');
  }
});

/** Filters with only the free text filled in. */
function filters(search: string): RequestFilters {
  return {
    search: search,
    status: [],
    priority: [],
    organizationName: '',
    assignedTo: '',
    createdFrom: '',
    createdTo: '',
  };
}

function page(items: RequestListItem[], totalCount: number): PagedResult<RequestListItem> {
  return { items: items, page: 1, pageSize: 20, totalCount: totalCount, totalPages: Math.ceil(totalCount / 20) };
}

function item(id: number): RequestListItem {
  return {
    id: id,
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
