import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { authInterceptor } from '../app/core/auth.interceptor';
import { AuthService } from '../app/core/auth.service';

describe('authInterceptor', () => {
  it('attaches Authorization and X-Tenant-Id when token present', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            token: () => 'header.payload.sig',
            tenantId: () => '00000000-0000-0000-0000-000000000000',
          },
        },
      ],
    });
    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);
    http.get('/test').subscribe();

    const req = ctrl.expectOne('/test');
    expect(req.request.headers.get('Authorization')).toBe('Bearer header.payload.sig');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('00000000-0000-0000-0000-000000000000');
    req.flush({ ok: true });
    ctrl.verify();
  });
});
