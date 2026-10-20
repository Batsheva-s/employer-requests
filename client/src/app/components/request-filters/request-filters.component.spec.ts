import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RequestFilters } from '../../models/request.models';
import { FILTER_DEBOUNCE_MS, RequestFiltersComponent } from './request-filters.component';

describe('RequestFiltersComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] }));
  afterEach(() => vi.useRealTimers());

  it('waits until the user stops typing, then emits the filters once', async () => {
    const fixture = TestBed.createComponent(RequestFiltersComponent);
    await fixture.whenStable();
    vi.useFakeTimers(); // from here on, time moves only when the test says so

    const emitted: RequestFilters[] = [];
    fixture.componentInstance.filtersChange.subscribe((filters) => emitted.push(filters));

    const search: HTMLInputElement = fixture.nativeElement.querySelector('input[formcontrolname="search"]');
    for (const text of ['א', 'אי', 'איש', 'אישור']) {
      search.value = text;
      search.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(100); // typing faster than the debounce time
    }

    expect(emitted).toEqual([]);

    vi.advanceTimersByTime(FILTER_DEBOUNCE_MS);

    expect(emitted.length).toBe(1);
    expect(emitted[0].search).toBe('אישור');
  });
});
