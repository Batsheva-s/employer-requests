import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'employer-requests.user-name';
const DEFAULT_NAME = 'משתמש לדוגמה';

/**
 * The name recorded in the history when this user changes a status, remembered in the browser between visits.
 * The exam has no login: with real authentication, the server would take the name from the user's token.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  readonly name = signal(readStoredName() ?? DEFAULT_NAME);

  setName(name: string): void {
    this.name.set(name.trim() || DEFAULT_NAME);
    try {
      localStorage.setItem(STORAGE_KEY, this.name());
    } catch {
      // Storage is not available (for example, a private window): the name is just not remembered.
    }
  }
}

function readStoredName(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)?.trim() || null;
  } catch {
    return null;
  }
}
