import { BulkItemOutcome, RequestPriority, RequestStatus } from '../models/request.models';

// The API speaks English enum names; the screen speaks Hebrew. One place for every label.

export const STATUS_LABELS: Record<RequestStatus, string> = {
  New: 'חדשה',
  InProgress: 'בטיפול',
  Waiting: 'ממתינה',
  Completed: 'הושלמה',
};

export const PRIORITY_LABELS: Record<RequestPriority, string> = {
  Low: 'נמוכה',
  Medium: 'בינונית',
  High: 'גבוהה',
};

export const BULK_OUTCOME_LABELS: Record<BulkItemOutcome, string> = {
  Updated: 'עודכנה',
  NotFound: 'לא נמצאה',
  Conflict: 'עודכנה בינתיים על ידי משתמש אחר',
  InvalidTransition: 'המעבר אינו מותר מהסטטוס הנוכחי',
};

/** "פנייה אחת" / "3 פניות". */
export function requestsCount(count: number): string {
  if (count === 1) {
    return 'פנייה אחת';
  }
  // toLocaleString: 1234 → "1,234"
  return `${count.toLocaleString('he-IL')} פניות`;
}
