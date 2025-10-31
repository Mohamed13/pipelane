import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, of, throwError } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { AiSuggestFollowupResponse, TemplateSummary } from '../app/core/models';
import { CampaignBuilderComponent } from '../app/features/campaigns/campaign-builder.component';

describe('CampaignBuilderComponent', () => {
  let fixture: ComponentFixture<CampaignBuilderComponent>;
  let component: CampaignBuilderComponent;
  let api: jest.Mocked<ApiService>;
  let snackbar: { open: jest.Mock };

  const templates: TemplateSummary[] = [
    {
      id: 'tmpl-1',
      tenantId: 'tenant-1',
      name: 'Welcome onboarding',
      channel: 'email',
      lang: 'fr',
      category: 'welcome',
      coreSchemaJson: '{}',
      isActive: true,
      updatedAtUtc: '2025-01-01T00:00:00Z',
    },
    {
      id: 'tmpl-2',
      tenantId: 'tenant-1',
      name: 'WhatsApp reminder',
      channel: 'whatsapp',
      lang: 'en',
      category: 'reminder',
      coreSchemaJson: '{}',
      isActive: true,
      updatedAtUtc: '2025-01-01T00:00:00Z',
    },
  ];

  const followupPreview: AiSuggestFollowupResponse = {
    proposalId: 'prop-1',
    scheduledAtIso: '2025-02-01T09:30:00Z',
    angle: 'reminder',
    previewText: 'Bonjour {firstName}, une petite nouvelle pour vous.',
  };

  beforeEach(async () => {
    api = {
      getTemplates: jest.fn().mockReturnValue(of(templates)),
      previewFollowups: jest.fn().mockReturnValue(of({ count: 128 })),
      suggestSmartFollowup: jest.fn().mockReturnValue(of(followupPreview)),
      createCampaign: jest.fn().mockReturnValue(of({ id: 'cmp-42' })),
    } as unknown as jest.Mocked<ApiService>;

    snackbar = { open: jest.fn() } as unknown as { open: jest.Mock };

    await TestBed.configureTestingModule({
      imports: [CampaignBuilderComponent],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackbar },
      ],
    })
      .overrideComponent(CampaignBuilderComponent, {
        set: { template: '', styleUrls: [] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(CampaignBuilderComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('generates segment JSON and refreshes preview when audience changes', () => {
    const segmentControl = component['audienceGroup'].get('segmentJson');
    expect(segmentControl?.value).toContain('"channels":["whatsapp"]');
    expect(api.previewFollowups).toHaveBeenCalledWith(
      expect.stringContaining('"channels":["whatsapp"]'),
    );

    component.toggleTag('VIP');
    const nextSegment = component['audienceGroup'].get('segmentJson')?.value as string;
    expect(JSON.parse(nextSegment).tags).toEqual(['VIP']);
    expect(api.previewFollowups).toHaveBeenLastCalledWith(nextSegment);
  });

  it('loads followup preview and falls back gracefully on error', () => {
    expect(component.followupPreview()).toEqual(followupPreview);
    expect(component.followupLoading()).toBe(false);

    api.suggestSmartFollowup.mockReturnValueOnce(throwError(() => new Error('fail')));
    component['loadFollowupPreview'](true);

    expect(component.followupPreview()).toBeNull();
    expect(component.followupLoading()).toBe(false);
  });

  it('computes summary payload when form is valid', () => {
    component.messageGroup.patchValue({
      templateId: 'tmpl-1',
      primaryChannel: 'email',
      fallbackOrder: ['whatsapp'],
      smartFollowupDefault: true,
    });
    component.scheduleGroup.patchValue({
      scheduledDate: new Date('2025-03-10'),
      scheduledTime: '10:15',
      batchSize: 120,
    });

    const summary = component.summary();
    expect(summary).toEqual(
      expect.objectContaining({
        name: 'Untitled campaign',
        primaryChannel: 'email',
        fallbackOrderJson: '["whatsapp"]',
        segmentJson: expect.stringContaining('"lastActivityDays":30'),
        scheduledAtUtc: '2025-03-10T10:15:00.000Z',
        smartFollowupDefault: true,
      }),
    );
  });

  it('creates a campaign and resets the form state on success', () => {
    component.messageGroup.patchValue({
      templateId: 'tmpl-2',
      primaryChannel: 'whatsapp',
    });

    component.createCampaign();

    expect(api.createCampaign).toHaveBeenCalledWith(
      expect.objectContaining({
        templateId: 'tmpl-2',
        primaryChannel: 'whatsapp',
      }),
    );
    expect(snackbar.open).toHaveBeenCalledWith('Campaign created (ID: cmp-42)', 'Close', {
      duration: 4000,
    });
    expect(component.previewCount()).toBeNull();
    expect(component.messageGroup.get('smartFollowupDefault')?.value).toBe(false);
  });

  it('shows an error toast when campaign creation fails', () => {
    api.createCampaign.mockReturnValueOnce(throwError(() => new Error('boom')));
    component.messageGroup.patchValue({
      templateId: 'tmpl-2',
      primaryChannel: 'whatsapp',
    });

    component.createCampaign();

    expect(snackbar.open).toHaveBeenCalledWith('Failed to create campaign', 'Dismiss', {
      duration: 4000,
    });
    expect(component.creating()).toBe(false);
  });

  it('cleans up subscribed observables on destroy', () => {
    let unsubscribed = false;
    api.suggestSmartFollowup.mockReturnValueOnce(
      new Observable<AiSuggestFollowupResponse>((observer) => {
        observer.next(followupPreview);
        return () => {
          unsubscribed = true;
        };
      }),
    );

    const localFixture = TestBed.createComponent(CampaignBuilderComponent);
    const instance = localFixture.componentInstance;
    expect(unsubscribed).toBe(false);
    instance.ngOnDestroy();
    localFixture.destroy();
    expect(unsubscribed).toBe(true);
  });
});
