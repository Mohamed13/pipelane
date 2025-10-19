import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ApiService } from '../app/core/api.service';
import { AuthService } from '../app/core/auth.service';
import { Channel, ChannelSettingsPayload } from '../app/core/models';

describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;
  const snackbar = { open: jest.fn() };
  const authMock = {
    tenantId: () => 'tenant-123',
    token: () => 'token',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authMock },
        { provide: MatSnackBar, useValue: snackbar },
      ],
    });

    service = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
    snackbar.open.mockReset();
  });

  afterEach(() => {
    http.verify();
  });

  it('adds tenant header when saving channel settings', () => {
    const payload: ChannelSettingsPayload = {
      channel: 'email',
      settings: { apiKey: 'test', from: 'ops@pipelane.app' },
    };

    service.saveChannelSettings(payload).subscribe();

    const req = http.expectOne('http://localhost:5000/onboarding/channel-settings');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    expect(req.request.method).toBe('POST');
    req.flush({ ok: true });
  });

  it('surfaces API errors via snackbar with detail message', () => {
    const subscribe = jest.fn();

    service.getTemplates().subscribe({ next: subscribe, error: () => undefined });

    const req = http.expectOne('http://localhost:5000/templates');
    req.flush({ detail: 'Validation failed' }, { status: 400, statusText: 'Bad Request' });

    expect(subscribe).not.toHaveBeenCalled();
    expect(snackbar.open).toHaveBeenCalledWith('Loading templates: Validation failed', 'Dismiss', {
      duration: 6000,
    });
  });

  it('calls followup preview with /api prefix', () => {
    service.previewFollowups('{}').subscribe();

    const req = http.expectOne('http://localhost:5000/api/followups/preview');
    expect(req.request.method).toBe('POST');
    req.flush({ count: 0 });
  });
});
