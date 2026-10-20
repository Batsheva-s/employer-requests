import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription, forkJoin } from 'rxjs';
import { describeHttpError, isConflict } from '../../../core/http/http-error.util';
import { STATUS_LABELS } from '../../../core/i18n/labels';
import { RequestDetails, RequestStatus, RequestStatusChange } from '../../../core/models/request.models';
import { RequestsApiService } from '../../../core/services/requests-api.service';
import { PriorityLabelPipe, StatusLabelPipe } from '../../../shared/labels.pipe';

export interface RequestDetailsDialogData {
  requestId: number;
}

/**
 * One request: its details, a status change, and its history.
 * The status change carries the version the user is looking at. If someone else changed the request in the
 * meantime, the server refuses (409): the dialog says so, loads the current state, and lets the user decide again.
 */
@Component({
  selector: 'app-request-details-dialog',
  imports: [
    DatePipe,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressBarModule,
    StatusLabelPipe,
    PriorityLabelPipe,
  ],
  templateUrl: './request-details-dialog.component.html',
  styleUrl: './request-details-dialog.component.css',
})
export class RequestDetailsDialogComponent {
  private readonly api = inject(RequestsApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly data = inject<RequestDetailsDialogData>(MAT_DIALOG_DATA);

  protected readonly request = signal<RequestDetails | null>(null);
  protected readonly history = signal<RequestStatusChange[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly newStatus = signal<RequestStatus | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  /** Shown after a 409: someone else changed the request, and the dialog now shows the current state. */
  protected readonly conflictMessage = signal<string | null>(null);

  private loadSubscription?: Subscription;

  constructor() {
    this.load();
  }

  /** Loads the request and its history together. A load still on its way is cancelled: only the newest counts. */
  protected load(): void {
    this.loadSubscription?.unsubscribe();
    this.loading.set(true);
    this.loadError.set(null);

    this.loadSubscription = forkJoin({ request: this.api.getById(this.data.requestId), history: this.api.getHistory(this.data.requestId) })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ request, history }) => {
          this.request.set(request);
          this.history.set(history);
          this.newStatus.set(request.allowedNextStatuses[0] ?? null);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loadError.set(describeHttpError(error));
          this.loading.set(false);
        },
      });
  }

  protected save(): void {
    const request = this.request();
    const newStatus = this.newStatus();
    if (!request || !newStatus) {
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.conflictMessage.set(null);

    // request.rowVersion is the version on the screen: the server saves only if it is still the current one.
    this.api
      .changeStatus(request.id, newStatus, request.rowVersion)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.snackBar.open(`הסטטוס עודכן ל"${STATUS_LABELS[updated.status]}"`, undefined, { duration: 3000 });
          this.load();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          if (isConflict(error)) {
            // Nothing of ours was saved. Show what is there now – the history says who changed it – and let the user decide.
            this.conflictMessage.set(
              'הפנייה עודכנה בינתיים על ידי משתמש אחר, ולכן השינוי שלך לא נשמר. מוצג עכשיו המצב העדכני – אפשר לבחור שוב.',
            );
            this.load();
          } else {
            this.saveError.set(describeHttpError(error));
          }
        },
      });
  }
}
