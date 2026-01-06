import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentUserStateService } from '../services/current-user-state.service';

/**
 * HTTP Interceptor that adds the X-Current-User header to all outgoing requests.
 * This is for demo purposes only - in production, user identity would come from authentication tokens.
 */
export const currentUserInterceptor: HttpInterceptorFn = (req, next) => {
  const currentUserState = inject(CurrentUserStateService);
  const currentUserId = currentUserState.currentUserId();

  // Only add header if we have a current user
  if (currentUserId) {
    const clonedRequest = req.clone({
      setHeaders: {
        'X-Current-User': currentUserId
      }
    });

    return next(clonedRequest);
  }

  return next(req);
};
