import { HttpErrorResponse } from '@angular/common/http';
import { STATUS_LABELS } from './labels';
import { RequestStatus } from '../models/request.models';

/** The parts of a Problem Details answer (RFC 9457) that the screen uses. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  allowedStatuses?: RequestStatus[];
}

/** API field names, as they appear in a 400 answer (in lower case), and what the user calls them. */
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

  const problem = readProblemDetails(error.error);

  switch (error.status) {
    case 400:
      return invalidFieldsMessage(problem);
    case 404:
      return 'הפנייה לא נמצאה.';
    case 409:
      return 'הפנייה עודכנה בינתיים על ידי משתמש אחר, ולכן השינוי שלך לא נשמר.';
    case 422:
      return transitionMessage(problem);
  }

  if (error.status >= 500) {
    return 'אירעה תקלה בשרת והפעולה לא בוצעה. נסו שוב בעוד רגע.';
  }
  return `הפעולה נכשלה (קוד ${error.status}).`;
}

/** 409: someone else changed the request after the user loaded it. */
export function isConflict(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409;
}

/** True when the API itself was not reached. */
function isServerUnreachable(error: HttpErrorResponse): boolean {
  // 0: no answer at all. 502, 503, 504: a gateway answered instead of the API.
  if (error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504) {
    return true;
  }

  // The API answers every 5xx with Problem Details. A 5xx without them came from somewhere else – for example,
  // the development server's proxy answers 500 with an empty body when the API is down.
  if (error.status >= 500 && readProblemDetails(error.error) === null) {
    return true;
  }

  return false;
}

/** The body as Problem Details, or null if it is not one (every Problem Details answer of the API has a title). */
function readProblemDetails(body: unknown): ProblemDetails | null {
  if (body !== null && typeof body === 'object' && 'title' in body) {
    return body as ProblemDetails;
  }
  return null;
}

/** "ערך לא תקין: שורות בעמוד, סטטוס." – every invalid field once, by its Hebrew name. */
function invalidFieldsMessage(problem: ProblemDetails | null): string {
  const labels: string[] = [];

  if (problem !== null && problem.errors) {
    for (const key of Object.keys(problem.errors)) {
      const label = fieldLabel(key);
      if (!labels.includes(label)) {
        labels.push(label);
      }
    }
  }

  if (labels.length === 0) {
    return 'הנתונים שנשלחו אינם תקינים.';
  }
  return `ערך לא תקין: ${labels.join(', ')}.`;
}

/** The Hebrew name of a field. The keys of a 400 answer look like "PageSize", "$.status" or "Items[3].RowVersion". */
function fieldLabel(key: string): string {
  let name = key;

  // "$.status" → "status" ("$." marks a field of the JSON body).
  if (name.startsWith('$.')) {
    name = name.substring(2);
  }

  // "Items[3].RowVersion" → "Items": the field's name ends at the first "." or "[".
  name = name.split('.')[0];
  name = name.split('[')[0];

  const label = FIELD_LABELS[name.toLowerCase()];
  if (label === undefined) {
    return key; // a field without a Hebrew name is shown as the server named it
  }
  return label;
}

/** After a 422: which statuses the request may move to instead. */
function transitionMessage(problem: ProblemDetails | null): string {
  if (problem === null || !problem.allowedStatuses || problem.allowedStatuses.length === 0) {
    return 'המעבר לסטטוס הזה אינו מותר.';
  }

  const labels = problem.allowedStatuses.map((status) => STATUS_LABELS[status]);
  return `המעבר לסטטוס הזה אינו מותר. אפשר לעבור ל: ${labels.join(', ')}.`;
}
