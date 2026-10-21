import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RequestListItem, SortDirection, SortField } from '../../models/request.models';
import { PriorityLabelPipe, StatusLabelPipe } from '../../pipes/labels.pipe';

/**
 * Shows one page of requests and reports what the user does: sort, open, select.
 * It holds no data of its own and never sorts – sorting is done by the server.
 */
@Component({
  selector: 'app-requests-table',
  imports: [MatTableModule, MatSortModule, MatCheckboxModule, DatePipe, StatusLabelPipe, PriorityLabelPipe],
  templateUrl: './requests-table.component.html',
  styleUrl: './requests-table.component.css',
})
export class RequestsTableComponent {
  readonly items = input.required<RequestListItem[]>();
  readonly sortBy = input.required<SortField>();
  readonly sortDirection = input.required<SortDirection>();
  readonly selectedIds = input.required<ReadonlySet<number>>();

  readonly sortChange = output<{ sortBy: SortField; sortDirection: SortDirection }>();
  readonly open = output<number>();
  readonly toggle = output<RequestListItem>();
  readonly toggleAll = output<void>();

  protected readonly columns = ['select', 'id', 'title', 'organizationName', 'status', 'priority', 'assignedTo', 'createdAt', 'updatedAt'];

  /** Every request on the page is selected: the header checkbox is checked. */
  protected readonly allSelected = computed(() => {
    const items = this.items();
    if (items.length === 0) {
      return false;
    }
    return items.every((item) => this.selectedIds().has(item.id));
  });

  /** Some, but not all, are selected: the header checkbox shows a dash. */
  protected readonly someSelected = computed(() => {
    if (this.allSelected()) {
      return false;
    }
    return this.items().some((item) => this.selectedIds().has(item.id));
  });

  protected onSort(sort: Sort): void {
    // matSortDisableClear: a header click always gives a direction – there is no "unsorted" state.
    if (sort.direction === '') {
      return;
    }

    // sort.active is the name of the column, and the columns that can be sorted are named after the sort fields.
    const sortBy = sort.active as SortField;
    this.sortChange.emit({ sortBy: sortBy, sortDirection: sort.direction });
  }
}
