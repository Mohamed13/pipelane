import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.token()) {
    return true;
  }
  const target = state?.url && !state.url.startsWith('/login') ? state.url : undefined;
  if (target) {
    return router.createUrlTree(['/login'], { queryParams: { redirect: target } }) as UrlTree;
  }
  return router.parseUrl('/login') as UrlTree;
};
