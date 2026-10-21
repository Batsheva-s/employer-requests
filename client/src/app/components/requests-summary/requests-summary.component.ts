import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RequestsSummary } from '../../models/request.models';
import { PriorityLabelPipe, StatusLabelPipe } from '../../pipes/labels.pipe';

/**
 * Shows the aggregations for the current filters: totals by status and by priority, and the busiest handlers.
 * It only displays what it receives; the state service loads the numbers.
 */
@Component({
  selector: 'app-requests-summary',
  imports: [DecimalPipe, DatePipe, MatProgressBarModule, StatusLabelPipe, PriorityLabelPipe],
  templateUrl: './requests-summary.component.html',
  styleUrl: './requests-summary.component.css',
})
export class RequestsSummaryComponent {
  readonly summary = input<RequestsSummary | null>(null);
  readonly loading = input(false);
  readonly errorMessage = input<string | null>(null);
}
