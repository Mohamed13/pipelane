import { fakeAsync, flushMicrotasks, TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { AiClassifyReplyResponse, ConversationResponse } from '../app/core/models';
import { PolicyService } from '../app/core/policy.service';
import { ConversationThreadComponent } from '../app/features/contacts/conversation-thread.component';

describe('ConversationThreadComponent (safety guards)', () => {
  let navigateMock: jest.Mock;
  let apiMock: {
    getConversation: jest.Mock;
    sendMessage: jest.Mock;
    generateAiMessage: jest.Mock;
    classifyAiReply: jest.Mock;
    validateFollowup: jest.Mock;
    getFollowupConversationPreview: jest.Mock;
  };

  beforeEach(() => {
    navigateMock = jest.fn().mockResolvedValue(true);
    apiMock = {
      getConversation: jest.fn().mockReturnValue(of({ conversationId: 'c1', messages: [] })),
      sendMessage: jest.fn().mockReturnValue(of({})),
      generateAiMessage: jest.fn().mockReturnValue(of({ text: 'hello' })),
      classifyAiReply: jest.fn().mockReturnValue(of({ classification: 'neutral' })),
      validateFollowup: jest.fn().mockReturnValue(of(void 0)),
      getFollowupConversationPreview: jest
        .fn()
        .mockReturnValue(of({ historySnippet: '', proposal: null })),
    };

    TestBed.configureTestingModule({
      imports: [ConversationThreadComponent],
      providers: [
        { provide: ApiService, useValue: apiMock },
        {
          provide: PolicyService,
          useValue: {
            isWhatsAppTextAllowed: jest.fn().mockReturnValue(true),
          },
        },
        { provide: Router, useValue: { navigate: navigateMock } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({}) } },
        },
        { provide: MatSnackBar, useValue: { open: jest.fn() } },
      ],
    });
  });

  it('redirects to contacts when route misses contactId', fakeAsync(() => {
    const fixture = TestBed.createComponent(ConversationThreadComponent);

    fixture.detectChanges();
    flushMicrotasks();

    expect(navigateMock).toHaveBeenCalledWith(['/contacts']);
    expect(apiMock.getConversation).not.toHaveBeenCalled();
    expect(fixture.componentInstance.contactId).toBe('');
  }));
});

describe('ConversationThreadComponent (follow-up preview)', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<ConversationThreadComponent>>;
  let component: ConversationThreadComponent;
  let apiMock: {
    getConversation: jest.Mock;
    sendMessage: jest.Mock;
    generateAiMessage: jest.Mock;
    classifyAiReply: jest.Mock;
    validateFollowup: jest.Mock;
    getFollowupConversationPreview: jest.Mock;
  };
  let snackbar: { open: jest.Mock };

  beforeEach(async () => {
    apiMock = {
      getConversation: jest.fn().mockReturnValue(
        of<ConversationResponse>({
          conversationId: 'conv-1',
          messages: [],
        }),
      ),
      sendMessage: jest.fn().mockReturnValue(of({})),
      generateAiMessage: jest.fn().mockReturnValue(of({ text: 'hello' })),
      classifyAiReply: jest.fn().mockReturnValue(of<AiClassifyReplyResponse | null>(null)),
      validateFollowup: jest.fn().mockReturnValue(of(void 0)),
      getFollowupConversationPreview: jest.fn().mockReturnValue(
        of({
          historySnippet: 'Client: Bonjour',
          proposal: {
            proposalId: 'prop-1',
            scheduledAtIso: '2025-01-02T09:00:00Z',
            angle: 'reminder',
            previewText: 'Relance test',
          },
        }),
      ),
    };
    snackbar = { open: jest.fn() };

    await TestBed.configureTestingModule({
      imports: [ConversationThreadComponent],
      providers: [
        { provide: ApiService, useValue: apiMock },
        {
          provide: PolicyService,
          useValue: { isWhatsAppTextAllowed: jest.fn().mockReturnValue(true) },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ contactId: 'conv-1' }) } },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: MatSnackBar, useValue: snackbar },
      ],
    })
      .overrideComponent(ConversationThreadComponent, {
        set: { template: '' },
      })
      .compileComponents();

    fixture = TestBed.createComponent(ConversationThreadComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => jest.clearAllMocks());

  it('requestSmartFollowup stores preview data', () => {
    component.followupPreview.set(null);
    component.followupHistorySnippet.set('');
    (component as unknown as { requestSmartFollowup: () => void }).requestSmartFollowup();

    expect(apiMock.getFollowupConversationPreview).toHaveBeenCalledWith('conv-1');
    expect(component.followupLoading()).toBe(false);
    expect(component.followupHistorySnippet()).toBe('Client: Bonjour');
    expect(component.followupPreview()).toEqual(
      expect.objectContaining({
        proposalId: 'prop-1',
        previewText: 'Relance test',
      }),
    );
  });

  it('requestSmartFollowup resets loading state on error', () => {
    component.followupPreview.set(null);
    apiMock.getFollowupConversationPreview.mockReturnValueOnce(throwError(() => new Error('fail')));

    (component as unknown as { requestSmartFollowup: () => void }).requestSmartFollowup();

    expect(component.followupLoading()).toBe(false);
    expect(component.followupPreview()).toBeNull();
  });

  it('validates followup proposal and clears cached preview', () => {
    component.followupPreview.set({
      proposalId: 'prop-1',
      scheduledAtIso: '2025-01-02T09:00:00Z',
      angle: 'reminder',
      previewText: 'Relance test',
    });
    component.followupHistorySnippet.set('Client: Bonjour');

    component.onValidateFollowup();

    expect(apiMock.validateFollowup).toHaveBeenCalledWith({
      conversationId: 'conv-1',
      proposalId: 'prop-1',
    });
    expect(component.followupLoading()).toBe(false);
    expect(component.followupPreview()).toBeNull();
    expect(component.followupHistorySnippet()).toBe('');
    expect(snackbar.open).toHaveBeenCalledWith('Relance programmée', 'Fermer', {
      duration: 4000,
    });
  });
});
