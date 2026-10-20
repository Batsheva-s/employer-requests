import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { describeHttpError } from '../../../core/http/http-error.util';
import { BULK_OUTCOME_LABELS, requestsCount } from '../../../core/i18n/labels';
import { BulkChangeStatusResult, RequestStatus } from '../../../core/models/request.models';
import { BulkStatusBarComponent } from '../bulk-status-bar/bulk-status-bar.component';
import { RequestDetailsDialogComponent, RequestDetailsDialogData } from '../request-details/request-details-dialog.component';
import { RequestFiltersComponent } from '../request-filters/request-filters.component';
import { RequestsSummaryComponent } from '../requests-summary/requests-summary.component';
import { RequestsTableComponent } from '../requests-table/requests-table.component';
import { RequestsStore } from '../requests.store';

/**
 * The requests screen. It connects the store to the presentational components:
 * the components show data and report user actions; the store keeps the state and calls the API.
 */
@Component({
  selector: 'app-requests-page',
  imports: [
    MatButtonModule,
    MatPaginatorModule,
    MatProgressBarModule,
    RequestsSummaryComponent,
    RequestFiltersComponent,
    BulkStatusBarComponent,
    RequestsTableComponent,
  ],
  providers: [RequestsStore],
  templateUrl: './requests-page.component.html',
  styleUrl: './requests-page.component.css',
})
export class RequestsPageComponent {
  protected readonly store = inject(RequestsStore);
  private readonly dialog = inject(MatDialog);

  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly outcomeLabels = BULK_OUTCOME_LABELS;

  /** Increased after every status change, so the summary loads its numbers again. */
  protected readonly summaryRefresh = signal(0);

  protected readonly bulkBusy = signal(false);
  protected readonly bulkResult = signal<BulkChangeStatusResult | null>(null);
  protected readonly bulkError = signal<string | null>(null);

  /** "עודכנו 3 פניות. לא עודכנה פנייה אחת:" */
  protected readonly bulkSummary = computed(() => {
    const result = this.bulkResult();
    if (!result) {
      return '';
    }
    const updated = result.succeeded === 0 ? 'לא עודכנה אף פנייה.' : `עודכנו ${requestsCount(result.succeeded)}.`;
    const failed = result.failed === 0 ? '' : ` לא עודכנו ${requestsCount(result.failed)}:`;
    return updated + failed;
  });

  /** The requests of the last bulk change that were not updated, each with the reason. */
  protected readonly bulkFailures = computed(() => this.bulkResult()?.items.filter((item) => item.outcome !== 'Updated') ?? []);

  protected onPage(event: PageEvent): void {
    this.store.setPage(event.pageIndex + 1, event.pageSize);
  }

  protected openDetails(requestId: number): void {
    this.dialog
      .open<RequestDetailsDialogComponent, RequestDetailsDialogData>(RequestDetailsDialogComponent, {
        data: { requestId },
        width: '760px',
        maxWidth: '95vw',
        direction: 'rtl',
        autoFocus: false,
      })
      .afterClosed()
      // Whatever happened in the dialog – a change, or a conflict that showed newer data – show the current state.
      .subscribe(() => this.refresh());
  }

  protected changeStatusOfSelected(status: RequestStatus): void {
    this.bulkBusy.set(true);
    this.bulkResult.set(null);
    this.bulkError.set(null);

    this.store.changeStatusOfSelected(status).subscribe({
      next: (result) => {
        this.bulkBusy.set(false);
        this.bulkResult.set(result);
        this.summaryRefresh.update((value) => value + 1);
      },
      error: (error: unknown) => {
        this.bulkBusy.set(false);
        this.bulkError.set(describeHttpError(error));
      },
    });
  }

  private refresh(): void {
    this.store.reload();
    this.summaryRefresh.update((value) => value + 1);
  }
}
