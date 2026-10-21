import { Injectable, computed, signal } from '@angular/core';

const STORAGE_KEY = 'employer-requests.user-name';
const DEFAULT_NAME = 'משתמש לדוגמה';

/**
 * Who is using the screen. The exam has no login, so the user types a name at the top of the screen; it is
 * remembered in the browser between visits, sent with every API call (currentUserInterceptor), and recorded by the
 * server in the history of every status change ("changed by"). With real authentication, the server would take
 * the name from the signed-in user instead.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  /** Exactly what the user typed – it may be empty while they are typing. */
  readonly name = signal(readStoredName());

  /** The name recorded in the history: what the user typed, or a default name when the field is empty. */
  readonly nameForHistory = computed(() => {
    const name = this.name().trim();
    if (name === '') {
      return DEFAULT_NAME;
    }
    return name;
  });

  setName(name: string): void {
    this.name.set(name);
    try {
      localStorage.setItem(STORAGE_KEY, name);
    } catch {
      // Storage is not available (for example, a private window): the name is just not remembered.
    }
  }
}

function readStoredName(): string {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === null) {
      return ''; // the first visit
    }
    return stored;
  } catch {
    return ''; // storage is not available
  }
}
