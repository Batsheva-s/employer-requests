import { Component, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, filter, map } from 'rxjs';
import {
  REQUEST_PRIORITIES,
  REQUEST_STATUSES,
  RequestFilters,
  RequestPriority,
  RequestStatus,
  sameValue,
} from '../../../core/models/request.models';
import { PriorityLabelPipe, StatusLabelPipe } from '../../../shared/labels.pipe';

/** How long the user must stop typing before the filters are applied. */
export const FILTER_DEBOUNCE_MS = 300;

/**
 * The filter bar. It only collects the user's input and emits the complete filters;
 * the page decides what to do with them.
 */
@Component({
  selector: 'app-request-filters',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, StatusLabelPipe, PriorityLabelPipe],
  templateUrl: './request-filters.component.html',
  styleUrl: './request-filters.component.css',
})
export class RequestFiltersComponent {
  /** Emits the complete filters once the user has stopped changing them. */
  readonly filtersChange = output<RequestFilters>();

  protected readonly statuses = REQUEST_STATUSES;
  protected readonly priorities = REQUEST_PRIORITIES;

  protected readonly form = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    status: new FormControl<RequestStatus[]>([], { nonNullable: true }),
    priority: new FormControl<RequestPriority[]>([], { nonNullable: true }),
    organizationName: new FormControl('', { nonNullable: true }),
    assignedTo: new FormControl('', { nonNullable: true }),
    createdFrom: new FormControl('', { nonNullable: true }),
    createdTo: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    this.form.valueChanges
      .pipe(
        // Debounce: wait until the user has stopped typing for 300 ms, then search once – not on every key press.
        debounceTime(FILTER_DEBOUNCE_MS),
        map(() => this.form.getRawValue()),
        filter((filters) => isDateRangeValid(filters)),
        // Filters equal to the last ones emitted are dropped (the store also ignores filters it already has).
        distinctUntilChanged(sameValue),
        takeUntilDestroyed(),
      )
      .subscribe((filters) => this.filtersChange.emit(filters));
  }

  protected get dateRangeInvalid(): boolean {
    return !isDateRangeValid(this.form.getRawValue());
  }

  protected clear(): void {
    this.form.reset();
  }
}

function isDateRangeValid(filters: RequestFilters): boolean {
  return !filters.createdFrom || !filters.createdTo || filters.createdFrom <= filters.createdTo;
}
