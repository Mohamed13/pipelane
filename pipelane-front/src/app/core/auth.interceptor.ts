import { HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';

export function authInterceptor(req: HttpRequest<any>, next: HttpHandlerFn): Observable<HttpEvent<any>> {
  const auth = inject(AuthService);
  const token = auth.token();
  const tenantId = auth.tenantId();
  if (token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}`, ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}) } });
  }
  return next(req);
}

