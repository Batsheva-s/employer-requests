import { HttpErrorResponse } from '@angular/common/http';
import { STATUS_LABELS } from '../i18n/labels';
import { RequestStatus } from '../models/request.models';

/** The parts of a Problem Details answer (RFC 9457) that the screen uses. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  allowedStatuses?: RequestStatus[];
}

/** API field names, as they appear in a 400 answer, and what the user calls them. */
const FIELD_LABELS: Record<string, string> = {
  search: 'חיפוש',
  status: 'סטטוס',
  priority: 'עדיפות',
  organizationname: 'ארגון',
  assignedto: 'מטפל/ת',
  createdfrom: 'מתאריך',
  createdto: 'עד תאריך',
  page: 'עמוד',
  pagesize: 'שורות בעמוד',
  sortby: 'מיון',
  sortdirection: 'כיוון המיון',
  rowversion: 'גרסת הפנייה',
  items: 'הפניות שנבחרו',
};

/** A Hebrew message for a failed API call, built from what the server answered. */
export function describeHttpError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'אירעה שגיאה לא צפויה. נסו לרענן את הדף.';
  }

  if (isServerUnreachable(error)) {
    return 'אין חיבור לשרת. ודאו שהשרת פועל ונסו שוב.';
  }

  const problem = asProblemDetails(error.error);
  switch (error.status) {
    case 400:
      return invalidFieldsMessage(problem);
    case 404:
      return 'הפנייה לא נמצאה.';
    case 409:
      return 'הפנייה עודכנה בינתיים על ידי משתמש אחר, ולכן השינוי שלך לא נשמר.';
    case 422:
      return transitionMessage(problem);
    default:
      return error.status >= 500
        ? 'אירעה תקלה בשרת והפעולה לא בוצעה. נסו שוב בעוד רגע.'
        : `הפעולה נכשלה (קוד ${error.status}).`;
  }
}

/** 409: someone else changed the request after the user loaded it. */
export function isConflict(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409;
}

/**
 * No answer at all (0), a gateway error, or a 5xx without Problem Details – which the API always sends – means the
 * API itself was not reached. (When the API is down, the development server's proxy answers 500 with an empty body.)
 */
function isServerUnreachable(error: HttpErrorResponse): boolean {
  if ([0, 502, 503, 504].includes(error.status)) {
    return true;
  }
  return error.status >= 500 && asProblemDetails(error.error) === null;
}

function asProblemDetails(body: unknown): ProblemDetails | null {
  return body !== null && typeof body === 'object' && 'title' in body ? (body as ProblemDetails) : null;
}

function invalidFieldsMessage(problem: ProblemDetails | null): string {
  const fields = [...new Set(Object.keys(problem?.errors ?? {}).map(fieldLabel))];
  return fields.length > 0 ? `ערך לא תקין: ${fields.join(', ')}.` : 'הנתונים שנשלחו אינם תקינים.';
}

/** Keys look like "PageSize", "$.status" or "Items[3].RowVersion". */
function fieldLabel(key: string): string {
  const name = key.replace(/^\$\./, '').split(/[.[]/)[0].toLowerCase();
  return FIELD_LABELS[name] ?? key;
}

function transitionMessage(problem: ProblemDetails | null): string {
  const allowed = (problem?.allowedStatuses ?? []).map((status) => STATUS_LABELS[status]);
  return allowed.length > 0
    ? `המעבר לסטטוס הזה אינו מותר. אפשר לעבור ל: ${allowed.join(', ')}.`
    : 'המעבר לסטטוס הזה אינו מותר.';
}
