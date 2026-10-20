import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { catchError, combineLatest, map, of, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../utils/http-errors';
import { RequestFilters, RequestsSummary } from '../../models/request.models';
import { RequestsApiService } from '../../services/requests-api.service';
import { PriorityLabelPipe, StatusLabelPipe } from '../../pipes/labels.pipe';

type SummaryOutcome = { summary: RequestsSummary | null; error: string | null };

/**
 * The aggregations for the current filters: totals by status and by priority, and the busiest handlers.
 * Loads again when the filters change, or when the page asks for it after a status change (refreshKey).
 */
@Component({
  selector: 'app-requests-summary',
  imports: [DecimalPipe, DatePipe, MatProgressBarModule, StatusLabelPipe, PriorityLabelPipe],
  templateUrl: './requests-summary.component.html',
  styleUrl: './requests-summary.component.css',
})
export class RequestsSummaryComponent {
  private readonly api = inject(RequestsApiService);

  readonly filters = input.required<RequestFilters>();
  /** Increase it to load the numbers again (after a status change). */
  readonly refreshKey = input(0);

  protected readonly summary = signal<RequestsSummary | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    // Same pattern as the list: switchMap keeps only the newest request alive.
    combineLatest([toObservable(this.filters), toObservable(this.refreshKey)])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.errorMessage.set(null);
        }),
        switchMap(([filters]) =>
          this.api.getSummary(filters).pipe(
            map((summary): SummaryOutcome => ({ summary, error: null })),
            catchError((error: unknown) => of<SummaryOutcome>({ summary: null, error: describeHttpError(error) })),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(({ summary, error }) => {
        this.loading.set(false);
        this.errorMessage.set(error);
        if (summary) {
          this.summary.set(summary);
        }
      });
  }
}
