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
import { describeHttpError, isConflict } from '../../utils/http-errors';
import { STATUS_LABELS } from '../../utils/labels';
import { RequestDetails, RequestStatus, RequestStatusChange } from '../../models/request.models';
import { CurrentUserService } from '../../services/current-user.service';
import { RequestsApiService } from '../../services/requests-api.service';
import { PriorityLabelPipe, StatusLabelPipe } from '../../pipes/labels.pipe';

export interface RequestDetailsDialogData {
  requestId: number;
}

/**
 * One request: its details, a status change, and its status history.
 * No status is preselected: the user always picks the next one. (The first allowed status is often the one the
 * request just came from, so a preselected value was one click away from undoing the change.)
 * The status change carries the version the user is looking at. If someone else changed the request in the
 * meantime, the server refuses (409): the dialog says so, loads the current state, and lets the user decide again.
 * After a successful change the history is loaded again, and its newest row – the change just made – is marked.
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
  protected readonly currentUser = inject(CurrentUserService);
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
  /** True after this user saved a change: the newest history row is that change, and it is marked. */
  protected readonly justSaved = signal(false);

  /** The load still on its way, if any. */
  private loadSubscription: Subscription | null = null;

  constructor() {
    this.load();
  }

  /** Loads the request and its history together. A load still on its way is cancelled: only the newest counts. */
  protected load(): void {
    if (this.loadSubscription !== null) {
      this.loadSubscription.unsubscribe(); // aborts its HTTP calls
    }
    this.loading.set(true);
    this.loadError.set(null);

    // forkJoin runs both calls at the same time, and answers once – when both have answered.
    const requestAndHistory = forkJoin({
      request: this.api.getById(this.data.requestId),
      history: this.api.getHistory(this.data.requestId),
    });

    this.loadSubscription = requestAndHistory
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (loaded) => {
          this.request.set(loaded.request);
          this.history.set(loaded.history);
          this.newStatus.set(null);
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
    if (request === null || newStatus === null) {
      return; // the button is disabled in this case; this is only a safety check
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.conflictMessage.set(null);
    this.justSaved.set(false);

    // request.rowVersion is the version on the screen: the server saves only if it is still the current one.
    this.api
      .changeStatus(request.id, newStatus, request.rowVersion)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.justSaved.set(true);
          // The answer carries the new version: the screen uses it at once, so the next change is based on it.
          this.request.set(updated);
          this.newStatus.set(null);
          const message = `הסטטוס עודכן ל"${STATUS_LABELS[updated.status]}" ונרשם בהיסטוריה על שם ${this.currentUser.nameForHistory()}`;
          this.snackBar.open(message, undefined, { duration: 4000 });
          // The history now has a new row – the change just made.
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
