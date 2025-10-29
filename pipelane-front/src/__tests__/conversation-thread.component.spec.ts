import { fakeAsync, flushMicrotasks, TestBed } from '@angular/core/testing';
import { convertToParamMap, ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ConversationThreadComponent } from '../app/features/contacts/conversation-thread.component';
import { ApiService } from '../app/core/api.service';
import { PolicyService } from '../app/core/policy.service';

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
