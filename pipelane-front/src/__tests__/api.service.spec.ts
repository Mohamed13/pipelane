import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ApiService } from '../app/core/api.service';
import { AuthService } from '../app/core/auth.service';
import {
  ChannelSettingsPayload,
  HunterSearchCriteria,
  ProspectListResponse,
  TopMessagesResponse,
} from '../app/core/models';

describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;
  let consoleSpy: jest.SpyInstance;
  let warnSpy: jest.SpyInstance;
  const snackbar = {
    dismiss: jest.fn(),
    openFromComponent: jest.fn(() => ({ onAction: () => ({ subscribe: () => undefined }) })),
    open: jest.fn(),
  };
  const authMock = {
    tenantId: () => 'tenant-123',
    token: () => 'token',
  };

  beforeEach(() => {
    consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => undefined);

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
    snackbar.dismiss.mockReset();
    snackbar.openFromComponent.mockClear();
    snackbar.open.mockClear();
  });

  afterEach(() => {
    consoleSpy.mockRestore();
    warnSpy.mockRestore();
    http.verify();
  });

  it('adds tenant header when saving channel settings', () => {
    const payload: ChannelSettingsPayload = {
      channel: 'email',
      settings: { apiKey: 'test', from: 'ops@pipelane.app' },
    };

    service.saveChannelSettings(payload).subscribe();

    const req = http.expectOne('https://localhost:56667/onboarding/channel-settings');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    expect(req.request.method).toBe('POST');
    req.flush({ ok: true });
  });

  it('surfaces API errors via snackbar with detail message', () => {
    const subscribe = jest.fn();

    service.getTemplates().subscribe({ next: subscribe, error: () => undefined });

    const req = http.expectOne('https://localhost:56667/templates');
    req.flush({ detail: 'Validation failed' }, { status: 400, statusText: 'Bad Request' });

    expect(subscribe).not.toHaveBeenCalled();
    expect(snackbar.dismiss).toHaveBeenCalled();
    expect(snackbar.openFromComponent).toHaveBeenCalledWith(expect.any(Function), {
      data: { context: 'Loading templates', detail: 'Validation failed' },
      duration: 7000,
      panelClass: ['error-snackbar'],
    });
  });

  it('calls followup preview with /api prefix', () => {
    service.previewFollowups('{}').subscribe();

    const req = http.expectOne('https://localhost:56667/api/followups/preview');
    expect(req.request.method).toBe('POST');
    req.flush({ count: 0 });
  });

  it('posts followup validation with tenant header', () => {
    service.validateFollowup({ conversationId: 'abc', proposalId: 'def' }).subscribe();

    const req = http.expectOne('https://localhost:56667/api/followups/validate');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    expect(req.request.body).toEqual({
      conversationId: 'abc',
      proposalId: 'def',
    });
    req.flush({ scheduledAt: new Date().toISOString(), conversationId: 'abc' });
  });

  it('fetches followup conversation preview via GET', () => {
    service.getFollowupConversationPreview('conv-1').subscribe();

    const req = http.expectOne((request) => request.url === 'https://localhost:56667/api/followups/preview');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('conversationId')).toBe('conv-1');
    req.flush({
      historySnippet: 'Vous: Bonjour',
      lastInteractionAt: new Date().toISOString(),
      read: true,
      timezone: 'Europe/Paris',
      proposal: {
        proposalId: 'proposal-123',
        scheduledAtIso: new Date().toISOString(),
        angle: 'value',
        previewText: 'On se reparle demain ?',
      },
    });
  });

  it('falls back to POST when GET followup preview is not allowed', () => {
    service.getFollowupConversationPreview('conv-fallback').subscribe();

    const getReq = http.expectOne('https://localhost:56667/api/followups/preview?conversationId=conv-fallback');
    expect(getReq.request.method).toBe('GET');
    getReq.flush('Method Not Allowed', { status: 405, statusText: 'Method Not Allowed' });

    const postReq = http.expectOne('https://localhost:56667/api/followups/preview');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({ conversationId: 'conv-fallback' });
    postReq.flush({
      historySnippet: 'Fallback history',
      lastInteractionAt: new Date().toISOString(),
      read: false,
      timezone: 'UTC',
      proposal: {
        proposalId: 'proposal-fallback',
        scheduledAtIso: new Date().toISOString(),
        angle: 'reminder',
        previewText: 'Hello again!',
      },
    });
  });

  it('shows toast when conversation id is missing for preview', () => {
    service.getFollowupConversationPreview('').subscribe({
      error: () => undefined,
    });
    expect(snackbar.open).toHaveBeenCalledWith(
      'Sélectionnez une conversation pour prévisualiser la relance.',
      undefined,
      expect.objectContaining({ duration: 5000 }),
    );
  });

  it('posts to demo endpoint when runDemo is called', () => {
    service.runDemo().subscribe();

    const req = http.expectOne('https://localhost:56667/api/demo/run');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    req.flush({ triggeredAtUtc: new Date().toISOString(), messages: [] });
  });

  it('loads report summary with explicit range', () => {
    service.getReportSummary('2025-01-01T00:00:00Z', '2025-01-07T23:59:59Z').subscribe();

    const req = http.expectOne((request) => request.url === 'https://localhost:56667/api/reports/summary');
    expect(req.request.params.get('from')).toBe('2025-01-01T00:00:00Z');
    expect(req.request.params.get('to')).toBe('2025-01-07T23:59:59Z');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    req.flush({
      from: '2025-01-01T00:00:00Z',
      to: '2025-01-07T23:59:59Z',
      totals: {
        queued: 0,
        sent: 0,
        delivered: 0,
        opened: 0,
        failed: 0,
        bounced: 0,
      },
      byChannel: [],
      topTemplates: [],
      meetingsBooked: 0,
    });
  });

  it('requests report summary pdf download', () => {
    service.downloadReportSummaryPdf('2025-01-01T00:00:00Z', '2025-01-07T23:59:59Z').subscribe();

    const req = http.expectOne((request) => request.url === 'https://localhost:56667/api/reports/summary.pdf');
    expect(req.request.params.get('from')).toBe('2025-01-01T00:00:00Z');
    expect(req.request.params.get('to')).toBe('2025-01-07T23:59:59Z');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob());
  });

  it('posts hunter search with optional dryRun flag', () => {
    const criteria: HunterSearchCriteria = {
      industry: 'Restaurants',
      source: 'mapsStub',
    };

    service.hunterSearch(criteria, { dryRun: true }).subscribe();

    const req = http.expectOne('https://localhost:56667/api/hunter/search?dryRun=true');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    expect(req.request.body).toEqual(criteria);
    req.flush({ total: 0, duplicates: 0, items: [] });
  });

  it('posts to seed demo endpoint', () => {
    service.seedHunterDemo().subscribe();

    const req = http.expectOne('https://localhost:56667/api/hunter/seed-demo');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Tenant-Id')).toBe('tenant-123');
    req.flush({ total: 50, duplicates: 0, items: [] });
  });

  it('normalizes null entries in top messages response', () => {
    let result: TopMessagesResponse | undefined;
    service.getTopMessages().subscribe((res) => (result = res));

    const req = http.expectOne((request) => request.url === 'https://localhost:56667/analytics/top-messages');
    expect(req.request.params.get('from')).toBe('');
    expect(req.request.params.get('to')).toBe('');
    req.flush({
      from: '2025-01-01T00:00:00Z',
      to: '2025-01-07T00:00:00Z',
      topByReplies: null,
      topByOpens: null,
    });

    expect(result?.topByReplies).toEqual([]);
    expect(result?.topByOpens).toEqual([]);
  });

  it('applies fallback labels when top messages omit label fields', () => {
    let result: TopMessagesResponse | undefined;
    service.getTopMessages().subscribe((res) => (result = res));

    const req = http.expectOne((request) => request.url === 'https://localhost:56667/analytics/top-messages');
    req.flush({
      from: '2025-01-01T00:00:00Z',
      to: '2025-01-07T00:00:00Z',
      topByReplies: [
        {
          key: 'template:a',
          label: null,
          channel: 'email',
          sent: 5,
          delivered: 4,
          opened: 3,
          failed: 0,
          bounced: 0,
          replies: 2,
        },
      ],
      topByOpens: [
        {
          key: null,
          label: '',
          channel: 'sms',
          sent: 2,
          delivered: 2,
          opened: 1,
          failed: 0,
          bounced: 0,
          replies: 0,
        },
      ],
    });

    expect(result?.topByReplies?.[0]?.label).toBe('(sans libellé)');
    expect(result?.topByOpens?.[0]?.label).toBe('(sans libellé)');
  });

  it('maps null reasons to empty arrays in list detail', () => {
    let response: ProspectListResponse | undefined;
    service.getList('list-42').subscribe((res) => (response = res));

    const req = http.expectOne('https://localhost:56667/api/lists/list-42');
    req.flush({
      id: 'list-42',
      name: null,
      createdAtUtc: '2025-01-01T00:00:00Z',
      updatedAtUtc: '2025-01-02T00:00:00Z',
      items: [
        {
          prospectId: 'prospect-1',
          prospect: {
            company: 'Acme',
            email: null,
            phone: null,
            firstName: null,
            lastName: null,
            website: null,
            city: null,
            country: null,
            whatsAppMsisdn: null,
            social: null,
          },
          score: 80,
          features: {
            rating: null,
            reviews: null,
            hasSite: null,
            booking: null,
            socialActive: null,
            cms: null,
            mobileOk: null,
            pixelPresent: null,
            lcpSlow: null,
          },
          why: null,
          addedAtUtc: '2025-01-02T00:00:00Z',
        },
      ],
    });

    expect(response?.items?.[0]?.why).toEqual([]);
  });

  it('returns empty list when API responds with null', () => {
    let lists: unknown;
    service.listSummaries().subscribe((res) => (lists = res));

    const req = http.expectOne('https://localhost:56667/api/lists');
    req.flush(null);

    expect(lists).toEqual([]);
  });

  it('ensures list summaries expose a fallback name', () => {
    let lists: unknown;
    service.listSummaries().subscribe((res) => (lists = res));

    const req = http.expectOne('https://localhost:56667/api/lists');
    req.flush([
      { id: 'list-1', name: null, count: 0, createdAtUtc: '2025-01-01', updatedAtUtc: '2025-01-01' },
      { id: 'list-2', name: '  ', count: 5, createdAtUtc: '2025-01-02', updatedAtUtc: '2025-01-03' },
      { id: 'list-3', name: 'Demand Gen', count: 7, createdAtUtc: '2025-01-02', updatedAtUtc: '2025-01-04' },
    ]);

    expect(lists).toEqual([
      { id: 'list-1', name: 'Sans titre', count: 0, createdAtUtc: '2025-01-01', updatedAtUtc: '2025-01-01' },
      { id: 'list-2', name: 'Sans titre', count: 5, createdAtUtc: '2025-01-02', updatedAtUtc: '2025-01-03' },
      { id: 'list-3', name: 'Demand Gen', count: 7, createdAtUtc: '2025-01-02', updatedAtUtc: '2025-01-04' },
    ]);
  });

  it('shows reconnect toast when list summaries returns 400', () => {
    let lists: unknown;
    service.listSummaries().subscribe((res) => (lists = res));

    const req = http.expectOne('https://localhost:56667/api/lists');
    req.flush({ title: 'tenant_header_missing' }, { status: 400, statusText: 'Bad Request' });

    expect(snackbar.open).toHaveBeenCalledWith(
      'Sélectionnez un espace de travail / reconnectez-vous.',
      undefined,
      expect.objectContaining({ duration: 5000 }),
    );
    expect(lists).toEqual([]);
  });
});

