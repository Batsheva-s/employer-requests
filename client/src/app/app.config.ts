import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import localeHe from '@angular/common/locales/he';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { currentUserInterceptor } from './interceptors/current-user.interceptor';
import { HebrewPaginatorIntl } from './utils/hebrew-paginator-intl';

// Hebrew dates and numbers for the date and number pipes.
registerLocaleData(localeHe);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // No zone.js: the screen updates when signals change.
    provideZonelessChangeDetection(),
    // withFetch: HttpClient on the browser's fetch API. Unsubscribing aborts the HTTP call (switchMap relies on it).
    provideHttpClient(withFetch(), withInterceptors([currentUserInterceptor])),
    { provide: LOCALE_ID, useValue: 'he' },
    { provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline', subscriptSizing: 'dynamic' } },
  ],
};
