import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { describeHttpError } from '../../utils/http-errors';
import { BULK_OUTCOME_LABELS, requestsCount } from '../../utils/labels';
import { BulkChangeStatusResult, RequestStatus } from '../../models/request.models';
import { BulkStatusBarComponent } from '../bulk-status-bar/bulk-status-bar.component';
import { RequestDetailsDialogComponent, RequestDetailsDialogData } from '../request-details-dialog/request-details-dialog.component';
import { RequestFiltersComponent } from '../request-filters/request-filters.component';
import { RequestsSummaryComponent } from '../requests-summary/requests-summary.component';
import { RequestsTableComponent } from '../requests-table/requests-table.component';
import { RequestsStateService } from '../../services/requests-state.service';

/**
 * The requests screen. It connects the state service to the components inside it:
 * the components show data and report what the user does; the state service keeps the state and calls the API.
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
  providers: [RequestsStateService],
  templateUrl: './requests-page.component.html',
  styleUrl: './requests-page.component.css',
})
export class RequestsPageComponent {
  protected readonly state = inject(RequestsStateService);
  private readonly dialog = inject(MatDialog);

  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly outcomeLabels = BULK_OUTCOME_LABELS;

  protected readonly bulkBusy = signal(false);
  protected readonly bulkResult = signal<BulkChangeStatusResult | null>(null);
  protected readonly bulkError = signal<string | null>(null);

  /** "עודכנו 3 פניות. לא עודכנה פנייה אחת:" */
  protected readonly bulkSummary = computed(() => {
    const result = this.bulkResult();
    if (result === null) {
      return '';
    }

    let text = '';
    if (result.succeeded === 0) {
      text = 'לא עודכנה אף פנייה.';
    } else {
      text = `עודכנו ${requestsCount(result.succeeded)}.`;
    }

    if (result.failed > 0) {
      text += ` לא עודכנו ${requestsCount(result.failed)}:`;
    }
    return text;
  });

  /** The requests of the last bulk change that were not updated, each with the reason. */
  protected readonly bulkFailures = computed(() => {
    const result = this.bulkResult();
    if (result === null) {
      return [];
    }
    return result.items.filter((item) => item.outcome !== 'Updated');
  });

  protected onPage(event: PageEvent): void {
    // The paginator counts pages from 0; the API counts them from 1.
    this.state.setPage(event.pageIndex + 1, event.pageSize);
  }

  protected openDetails(requestId: number): void {
    const data: RequestDetailsDialogData = { requestId: requestId };
    const dialogRef = this.dialog.open(RequestDetailsDialogComponent, {
      data: data,
      width: '760px',
      maxWidth: '95vw',
      direction: 'rtl',
      autoFocus: false,
    });

    // Whatever happened in the dialog – a change, or a conflict that showed newer data – show the current state.
    dialogRef.afterClosed().subscribe(() => {
      this.state.reload();
    });
  }

  protected changeStatusOfSelected(status: RequestStatus): void {
    this.bulkBusy.set(true);
    this.bulkResult.set(null);
    this.bulkError.set(null);

    this.state.changeStatusOfSelected(status).subscribe({
      next: (result) => {
        this.bulkBusy.set(false);
        this.bulkResult.set(result);
      },
      error: (error: unknown) => {
        this.bulkBusy.set(false);
        this.bulkError.set(describeHttpError(error));
        // Requests handled before the failure may already be saved: load the current state.
        this.state.reload();
      },
    });
  }
}
