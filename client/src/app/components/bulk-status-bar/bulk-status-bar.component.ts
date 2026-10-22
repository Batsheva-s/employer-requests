import { Component, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { requestsCount } from '../../utils/labels';
import { RequestStatus } from '../../models/request.models';
import { StatusLabelPipe } from '../../pipes/labels.pipe';

/**
 * Appears while requests are selected: the user picks one status for all of them. The bar only reports the choice
 * (apply); the page sends the change.
 * No status is preselected, as in the request dialog: one click must never change many requests to a status
 * the user did not choose.
 */
@Component({
  selector: 'app-bulk-status-bar',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule, StatusLabelPipe],
  templateUrl: './bulk-status-bar.component.html',
  styleUrl: './bulk-status-bar.component.css',
})
export class BulkStatusBarComponent {
  /** The same limit the server enforces. */
  protected readonly maxItems = 100;
  /** No request may move back to "New" (StatusWorkflow), so it is not offered. */
  protected readonly statuses: RequestStatus[] = ['InProgress', 'Waiting', 'Completed'];

  readonly count = input.required<number>();
  readonly busy = input(false);

  readonly apply = output<RequestStatus>();
  readonly clear = output<void>();

  /** Null until the user picks a status; until then the update button is disabled. */
  protected readonly newStatus = signal<RequestStatus | null>(null);

  /** "נבחרה פנייה אחת" / "נבחרו 3 פניות" */
  protected readonly selectedText = computed(() => {
    if (this.count() === 1) {
      return 'נבחרה פנייה אחת';
    }
    return `נבחרו ${requestsCount(this.count())}`;
  });

  protected applyNewStatus(): void {
    const status = this.newStatus();
    if (status === null) {
      return; // the button is disabled in this case; this is only a safety check
    }
    this.apply.emit(status);
  }
}
