import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { currentUserInterceptor } from '../../interceptors/current-user.interceptor';
import { RequestDetails, RequestStatusChange } from '../../models/request.models';
import { CurrentUserService } from '../../services/current-user.service';
import { RequestDetailsDialogComponent } from './request-details-dialog.component';

describe('RequestDetailsDialogComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([currentUserInterceptor])),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { requestId: 7 } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear(); // the user name is remembered in the browser
  });

  it('sends the version on the screen, and on 409 explains the conflict and shows the current state', async () => {
    const fixture = TestBed.createComponent(RequestDetailsDialogComponent);
    respondWithRequest(details('New', 'version-1'), []);
    await fixture.whenStable();

    clickButton(fixture.nativeElement, 'עדכון סטטוס');

    // The change carries the version the user saw. Someone else saved first: the server answers 409.
    const change = http.expectOne((request) => request.method === 'PATCH' && request.url === '/api/requests/7/status');
    expect(change.request.body).toEqual({ status: 'InProgress', rowVersion: 'version-1' });
    change.flush({ title: 'The request was changed by another user' }, { status: 409, statusText: 'Conflict' });

    // The dialog loads what is there now, including who made the change.
    const otherChange: RequestStatusChange = {
      id: 1,
      previousStatus: 'New',
      newStatus: 'Waiting',
      changedBy: 'רחל לוי',
      changedAt: '2026-10-20T10:00:00+00:00',
    };
    respondWithRequest(details('Waiting', 'version-2'), [otherChange]);
    await fixture.whenStable();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('עודכנה בינתיים על ידי משתמש אחר');
    expect(text).toContain('רחל לוי');
  });

  it('records the change under the user name from the top of the screen, and marks it in the history', async () => {
    TestBed.inject(CurrentUserService).setName('דנה');
    const fixture = TestBed.createComponent(RequestDetailsDialogComponent);
    respondWithRequest(details('New', 'version-1'), []);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('יירשם בהיסטוריה על שם: דנה');
    clickButton(fixture.nativeElement, 'עדכון סטטוס');

    // The name travels in a header (URL-encoded, since headers are ASCII); the server records it.
    const change = http.expectOne((request) => request.method === 'PATCH' && request.url === '/api/requests/7/status');
    expect(change.request.headers.get('X-User-Name')).toBe(encodeURIComponent('דנה'));
    change.flush(details('InProgress', 'version-2'));

    const saved: RequestStatusChange = {
      id: 1,
      previousStatus: 'New',
      newStatus: 'InProgress',
      changedBy: 'דנה',
      changedAt: '2026-10-20T10:00:00+00:00',
    };
    respondWithRequest(details('InProgress', 'version-2'), [saved]);
    await fixture.whenStable();

    const newestRow: HTMLElement | null = fixture.nativeElement.querySelector('.history tbody tr');
    expect(newestRow?.classList).toContain('just-saved');
    expect(newestRow?.textContent).toContain('דנה');
  });

  function respondWithRequest(request: RequestDetails, history: RequestStatusChange[]): void {
    http.expectOne('/api/requests/7').flush(request);
    http.expectOne('/api/requests/7/history').flush(history);
  }
});

function details(status: RequestDetails['status'], rowVersion: string): RequestDetails {
  return {
    id: 7,
    title: 'בירור תשלום',
    organizationName: 'אלון תעשיות בע"מ',
    status,
    priority: 'High',
    assignedTo: 'שרה כהן',
    createdAt: '2026-10-01T08:00:00+00:00',
    updatedAt: '2026-10-01T08:00:00+00:00',
    rowVersion,
    allowedNextStatuses: status === 'New' ? ['InProgress', 'Waiting'] : ['InProgress', 'Completed'],
  };
}

function clickButton(root: HTMLElement, text: string): void {
  const button = [...root.querySelectorAll('button')].find((candidate) => candidate.textContent?.trim() === text);
  if (!button) {
    throw new Error(`No button "${text}"`);
  }
  button.click();
}
