import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError, isConflict } from './http-errors';

describe('describeHttpError', () => {
  it('names the invalid fields of a 400 in Hebrew', () => {
    const error = errorResponse(400, {
      title: 'One or more validation errors occurred.',
      errors: { PageSize: ['The field PageSize must be between 1 and 100.'], '$.status': ['The input was not valid.'] },
    });

    expect(describeHttpError(error)).toBe('ערך לא תקין: שורות בעמוד, סטטוס.');
  });

  it('lists the statuses the workflow allows after a 422', () => {
    const error = errorResponse(422, { title: 'Status change not allowed', allowedStatuses: ['InProgress', 'Waiting'] });

    expect(describeHttpError(error)).toBe('המעבר לסטטוס הזה אינו מותר. אפשר לעבור ל: בטיפול, ממתינה.');
  });

  it('explains a 409 as a change made by another user', () => {
    const error = errorResponse(409, { title: 'The request was changed by another user' });

    expect(describeHttpError(error)).toContain('עודכנה בינתיים על ידי משתמש אחר');
    expect(isConflict(error)).toBe(true);
  });

  // it.each runs the same test once for every value in the list.
  it.each([0, 502, 503, 504])('treats status %i as "the server cannot be reached"', (status) => {
    expect(describeHttpError(errorResponse(status, null))).toBe('אין חיבור לשרת. ודאו שהשרת פועל ונסו שוב.');
  });

  it('treats a 500 without Problem Details as "the server cannot be reached", and a 500 with them as a server error', () => {
    expect(describeHttpError(errorResponse(500, null))).toBe('אין חיבור לשרת. ודאו שהשרת פועל ונסו שוב.');
    expect(describeHttpError(errorResponse(500, { title: 'An unexpected error occurred' }))).toContain('תקלה בשרת');
  });
});

/** A failed HTTP answer, as HttpClient reports it. */
function errorResponse(status: number, body: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status: status, error: body });
}
