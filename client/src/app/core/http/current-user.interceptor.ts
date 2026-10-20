import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentUserService } from '../services/current-user.service';

/**
 * Adds the user's name to every API call, so the server can record who changed what.
 * The name is URL-encoded: HTTP headers carry ASCII only, and names may be in Hebrew.
 */
export const currentUserInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const userName = inject(CurrentUserService).name();
  return next(request.clone({ setHeaders: { 'X-User-Name': encodeURIComponent(userName) } }));
};
