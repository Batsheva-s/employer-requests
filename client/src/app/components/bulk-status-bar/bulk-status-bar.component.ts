import { Component, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { requestsCount } from '../../utils/labels';
import { REQUEST_STATUSES, RequestStatus } from '../../models/request.models';
import { StatusLabelPipe } from '../../pipes/labels.pipe';

/** Appears while requests are selected: pick one status and apply it to all of them. */
@Component({
  selector: 'app-bulk-status-bar',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule, StatusLabelPipe],
  templateUrl: './bulk-status-bar.component.html',
  styleUrl: './bulk-status-bar.component.css',
})
export class BulkStatusBarComponent {
  /** The same limit the server enforces. */
  protected readonly maxItems = 100;
  protected readonly statuses = REQUEST_STATUSES;

  readonly count = input.required<number>();
  readonly busy = input(false);

  readonly apply = output<RequestStatus>();
  readonly clear = output<void>();

  protected readonly newStatus = signal<RequestStatus>('InProgress');
  protected readonly selectedText = computed(() =>
    this.count() === 1 ? 'נבחרה פנייה אחת' : `נבחרו ${requestsCount(this.count())}`,
  );
}
