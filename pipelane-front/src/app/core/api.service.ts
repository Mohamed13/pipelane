import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable, isDevMode } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, throwError, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { AuthService } from './auth.service';
import { environment } from './environment';
import { ErrorToastComponent } from './error-toast/error-toast.component';
import {
  CampaignCreatePayload,
  CampaignDetail,
  ChannelSettingsPayload,
  ConversationResponse,
  DeliveryAnalyticsResponse,
  FollowupPreviewResponse,
  PagedContactsResponse,
  ProspectImportResult,
  ProspectRecord,
  SendMessageRequestPayload,
  SendMessageResponse,
  TemplateSummary,
  ProspectingSequence,
  ProspectingSequencePayload,
  ProspectingCampaign,
  ProspectingCampaignCreateRequest,
  ProspectingCampaignPreview,
  ProspectingAnalyticsResponse,
  ProspectReplyRecord,
  ReplyIntent,
  GenerateProspectingEmailRequest,
  GenerateProspectingEmailResponse,
  ClassifyReplyResponse,
  AutoReplyResponse,
  AiGenerateMessageRequest,
  AiGenerateMessageResponse,
  AiClassifyReplyRequest,
  AiClassifyReplyResponse,
  AiSuggestFollowupRequest,
  AiSuggestFollowupResponse,
  DemoRunResponse,
  ReportSummaryResponse,
  ValidateFollowupRequestPayload,
  ValidateFollowupResponse,
  FollowupConversationPreviewResponse,
  TopMessagesResponse,
  TopMessageItem,
  HunterSearchCriteria,
  HunterSearchResponse,
  CreateListPayload,
  AddToListPayload,
  AddToListResponse,
  ProspectListResponse,
  ListSummary,
  CadenceFromListPayload,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly snackbar = inject(MatSnackBar, { optional: true });
  private readonly base = environment.API_BASE_URL;
  private readonly warnCache = new Set<string>();
  private readonly isDev = isDevMode();

  private headers(explicitTenantId?: string): Record<string, string> {
    const tenantId = explicitTenantId ?? this.auth.tenantId();
    return tenantId ? { 'X-Tenant-Id': tenantId } : {};
  }

  private warnOnce(key: string, detail?: unknown): void {
    if (!this.isDev) {
      return;
    }
    if (this.warnCache.has(key)) {
      return;
    }
    this.warnCache.add(key);
    if (typeof console !== 'undefined') {
      console.warn(`[API] ${key}`, detail);
    }
  }

  private openInfo(message: string): void {
    if (!this.snackbar) {
      return;
    }
    this.snackbar.dismiss();
    this.snackbar.open(message, undefined, {
      duration: 5000,
      panelClass: ['info-snackbar'],
    });
  }

  private handleError(context: string) {
    return (error: HttpErrorResponse) => {
      const detail = this.extractErrorMessage(error);
      if (this.isDev && typeof console !== 'undefined') {
        console.error(`[API] ${context}`, error);
      }
      if (this.snackbar) {
        this.snackbar.dismiss();
        this.snackbar.openFromComponent(ErrorToastComponent, {
          data: { context, detail },
          duration: 7000,
          panelClass: ['error-snackbar'],
        });
      }
      return throwError(() => error);
    };
  }

  private extractErrorMessage(error: HttpErrorResponse): string {
    if (error.error) {
      if (typeof error.error === 'string') {
        return error.error;
      }
      if (typeof error.error === 'object') {
        const candidate =
          error.error.detail ?? error.error.message ?? error.error.error ?? error.error.title;
        if (candidate) {
          return candidate;
        }
        try {
          return JSON.stringify(error.error);
        } catch {
          // ignore
        }
      }
    }
    if (error.status && error.statusText) {
      return `${error.status} ${error.statusText}`;
    }
    return error.message || 'Unexpected error';
  }

  saveChannelSettings(body: ChannelSettingsPayload, tenantId?: string): Observable<unknown> {
    return this.http
      .post(`${this.base}/onboarding/channel-settings`, body, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Saving channel settings')));
  }

  getTemplates(tenantId?: string): Observable<TemplateSummary[]> {
    return this.http
      .get<TemplateSummary[]>(`${this.base}/templates`, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading templates')));
  }

  refreshTemplates(tenantId?: string): Observable<{ updated: number }> {
    return this.http
      .post<{
        updated: number;
      }>(`${this.base}/templates/refresh`, {}, { headers: this.headers(tenantId) })
      .pipe(catchError(this.handleError('Refreshing templates')));
  }

  searchContacts(
    q: string,
    page = 1,
    size = 20,
    tenantId?: string,
  ): Observable<PagedContactsResponse> {
    const params = new HttpParams()
      .set('search', q)
      .set('page', page.toString())
      .set('size', size.toString());
    return this.http
      .get<PagedContactsResponse>(`${this.base}/contacts`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Searching contacts')));
  }

  getConversation(
    contactId: string,
    last = 50,
    tenantId?: string,
  ): Observable<ConversationResponse> {
    const params = new HttpParams().set('last', last.toString());
    return this.http
      .get<ConversationResponse>(`${this.base}/conversations/${contactId}`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading conversation')));
  }

  sendMessage(body: SendMessageRequestPayload, tenantId?: string): Observable<SendMessageResponse> {
    return this.http
      .post<SendMessageResponse>(`${this.base}/messages/send`, body, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Sending message')));
  }

  createCampaign(body: CampaignCreatePayload, tenantId?: string): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(`${this.base}/campaigns`, body, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Creating campaign')));
  }

  getCampaign(id: string, tenantId?: string): Observable<CampaignDetail> {
    return this.http
      .get<CampaignDetail>(`${this.base}/campaigns/${id}`, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading campaign')));
  }

  getDeliveryAnalytics(
    from?: string,
    to?: string,
    tenantId?: string,
  ): Observable<DeliveryAnalyticsResponse> {
    const params = new HttpParams({
      fromObject: {
        from: from ?? '',
        to: to ?? '',
      },
    });
    return this.http
      .get<DeliveryAnalyticsResponse>(`${this.base}/analytics/delivery`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(
        map((res) => ({
          ...res,
          byChannel: res.byChannel ?? [],
          byTemplate: res.byTemplate ?? [],
          timeline: res.timeline ?? [],
        })),
        catchError(this.handleError('Loading delivery analytics')),
      );
  }

  getTopMessages(
    from?: string,
    to?: string,
    tenantId?: string,
  ): Observable<TopMessagesResponse> {
    const params = new HttpParams({
      fromObject: {
        from: from ?? '',
        to: to ?? '',
      },
    });
    return this.http
      .get<TopMessagesResponse>(`${this.base}/analytics/top-messages`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(
        map((res) => {
          const normalize = (source: TopMessageItem[] | null | undefined) =>
            (source ?? []).map((item) => ({
              ...item,
              label: item.label?.trim() || '(sans libellé)',
            }));
          return {
            ...res,
            topByReplies: normalize(res.topByReplies),
            topByOpens: normalize(res.topByOpens),
          };
        }),
        catchError(this.handleError('Loading top messages')),
      );
  }

  getReportSummary(from?: string, to?: string, tenantId?: string): Observable<ReportSummaryResponse> {
    const params = new HttpParams({
      fromObject: {
        from: from ?? '',
        to: to ?? '',
      },
    });
    return this.http
      .get<ReportSummaryResponse>( `${this.base}/api/reports/summary`, { 
        params,
        headers: this.headers(tenantId),
      })
      .pipe(
        map((res) => ({
          ...res,
          byChannel: res.byChannel ?? [],
          topTemplates: res.topTemplates ?? [],
        })),
        catchError(this.handleError('Load report summary')),
      );
  }

  downloadReportSummaryPdf(from?: string, to?: string, tenantId?: string): Observable<Blob> {
    const params = new HttpParams({
      fromObject: {
        from: from ?? '',
        to: to ?? '',
      },
    });
    return this.http
      .get<Blob>( `${this.base}/api/reports/summary.pdf`, { 
        params,
        headers: this.headers(tenantId),
        responseType: 'blob' as 'json',
      })
      .pipe(catchError(this.handleError('Download report summary')));
  }

  getProspects(
    page = 1,
    size = 50,
    search?: string,
    tenantId?: string,
  ): Observable<{ total: number; items: ProspectRecord[] }> {
    let params = new HttpParams().set('page', page.toString()).set('size', size.toString());
    if (search) {
      params = params.set('search', search);
    }
    return this.http
      .get<{ total: number; items: ProspectRecord[] }>(`${this.base}/api/prospects`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading prospects')));
  }

  importProspects(
    payload: {
      kind: 'csv' | 'json';
      payloadBase64: string;
      fieldMap?: Record<string, string>;
      overwriteExisting?: boolean;
    },
    tenantId?: string,
  ): Observable<ProspectImportResult> {
    return this.http
      .post<ProspectImportResult>(`${this.base}/api/prospects/import`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Importing prospects')));
  }

  optOutProspect(email: string, tenantId?: string): Observable<ProspectRecord> {
    return this.http
      .post<ProspectRecord>(
        `${this.base}/api/prospects/optout`,
        {},
        {
          params: new HttpParams().set('email', email),
          headers: this.headers(tenantId),
        },
      )
      .pipe(catchError(this.handleError('Opting out prospect')));
  }

  getProspectingSequences(tenantId?: string): Observable<ProspectingSequence[]> {
    return this.http
      .get<ProspectingSequence[]>(`${this.base}/api/prospecting/sequences`, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading sequences')));
  }

  createProspectingSequence(
    payload: ProspectingSequencePayload,
    tenantId?: string,
  ): Observable<ProspectingSequence> {
    return this.http
      .post<ProspectingSequence>(`${this.base}/api/prospecting/sequences`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Creating sequence')));
  }

  updateProspectingSequence(
    id: string,
    payload: ProspectingSequencePayload,
    tenantId?: string,
  ): Observable<ProspectingSequence> {
    return this.http
      .put<ProspectingSequence>(`${this.base}/api/prospecting/sequences/${id}`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Updating sequence')));
  }

  getProspectingCampaigns(tenantId?: string): Observable<ProspectingCampaign[]> {
    return this.http
      .get<ProspectingCampaign[]>(`${this.base}/api/prospecting/campaigns`, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading prospecting campaigns')));
  }

  createProspectingCampaign(
    payload: ProspectingCampaignCreateRequest,
    tenantId?: string,
  ): Observable<ProspectingCampaign> {
    return this.http
      .post<ProspectingCampaign>(`${this.base}/api/prospecting/campaigns`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Creating prospecting campaign')));
  }

  getProspectingCampaign(id: string, tenantId?: string): Observable<ProspectingCampaign> {
    return this.http
      .get<ProspectingCampaign>(`${this.base}/api/prospecting/campaigns/${id}`, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading prospecting campaign')));
  }

  startProspectingCampaign(id: string, tenantId?: string): Observable<ProspectingCampaign> {
    return this.http
      .post<ProspectingCampaign>(
        `${this.base}/api/prospecting/campaigns/${id}/start`,
        {},
        { headers: this.headers(tenantId) },
      )
      .pipe(catchError(this.handleError('Starting prospecting campaign')));
  }

  pauseProspectingCampaign(id: string, tenantId?: string): Observable<ProspectingCampaign> {
    return this.http
      .post<ProspectingCampaign>(
        `${this.base}/api/prospecting/campaigns/${id}/pause`,
        {},
        { headers: this.headers(tenantId) },
      )
      .pipe(catchError(this.handleError('Pausing prospecting campaign')));
  }

  previewProspectingCampaign(
    id: string,
    tenantId?: string,
  ): Observable<ProspectingCampaignPreview> {
    return this.http
      .get<ProspectingCampaignPreview>(`${this.base}/api/prospecting/campaigns/${id}/preview`, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Previewing prospecting campaign')));
  }

  getProspectingAnalytics(
    from?: string,
    to?: string,
    tenantId?: string,
  ): Observable<ProspectingAnalyticsResponse> {
    const params = new HttpParams({
      fromObject: {
        from: from ?? '',
        to: to ?? '',
      },
    });
    return this.http
      .get<ProspectingAnalyticsResponse>(`${this.base}/api/prospecting/analytics`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading prospecting analytics')));
  }

  getProspectingReplies(intent?: ReplyIntent, tenantId?: string): Observable<ProspectReplyRecord[]> {
    let params = new HttpParams();
    if (intent && intent !== 'unknown') {
      params = params.set('intent', intent);
    }
    return this.http
      .get<ProspectReplyRecord[]>(`${this.base}/api/prospecting/replies`, {
        params,
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Loading replies')));
  }

  generateProspectingEmail(
    payload: GenerateProspectingEmailRequest,
    tenantId?: string,
  ): Observable<GenerateProspectingEmailResponse> {
    return this.http
      .post<GenerateProspectingEmailResponse>(`${this.base}/api/prospecting/ai/generate-email`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Generating prospecting email')));
  }

  classifyProspectReply(
    payload: { replyId: string },
    tenantId?: string,
  ): Observable<ClassifyReplyResponse> {
    return this.http
      .post<ClassifyReplyResponse>(`${this.base}/api/prospecting/ai/classify-reply`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Classifying reply')));
  }

  autoReplyDraft(
    payload: { replyId: string; campaignId?: string | null },
    tenantId?: string,
  ): Observable<AutoReplyResponse> {
    return this.http
      .post<AutoReplyResponse>(`${this.base}/api/prospecting/ai/auto-reply`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Generating auto-reply')));
  }

  generateAiMessage(
    payload: AiGenerateMessageRequest,
    tenantId?: string,
  ): Observable<AiGenerateMessageResponse> {
    return this.http
      .post<AiGenerateMessageResponse>(`${this.base}/api/ai/generate-message`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError("Générer un message")));
  }

  classifyAiReply(
    payload: AiClassifyReplyRequest,
    tenantId?: string,
  ): Observable<AiClassifyReplyResponse> {
    return this.http
      .post<AiClassifyReplyResponse>(`${this.base}/api/ai/classify-reply`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError("Classer la réponse")));
  }

  suggestSmartFollowup(
    payload: AiSuggestFollowupRequest,
    tenantId?: string,
  ): Observable<AiSuggestFollowupResponse> {
    return this.http
      .post<AiSuggestFollowupResponse>(`${this.base}/api/ai/suggest-followup`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Calculer la relance')));
  }

  triggerProspectingHook(
    slug: 'enrich' | 'send-next' | 'follow-up',
    tenantId?: string,
  ): Observable<Record<string, unknown>> {
    return this.http
      .post<Record<string, unknown>>(
        `${this.base}/api/prospecting/hooks/${slug}`,
        {},
        { headers: this.headers(tenantId) },
      )
      .pipe(catchError(this.handleError('Triggering automation hook')));
  }

  previewFollowups(segmentJson?: string, tenantId?: string): Observable<FollowupPreviewResponse> {
    return this.http
      .post<FollowupPreviewResponse>(
        `${this.base}/api/followups/preview`,
        { segmentJson: segmentJson ?? '{}' },
        { headers: this.headers(tenantId) },
      )
      .pipe(catchError(this.handleError('Previewing follow-ups')));
  }

  validateFollowup(
    payload: ValidateFollowupRequestPayload,
    tenantId?: string,
  ): Observable<ValidateFollowupResponse> {
    return this.http
      .post<ValidateFollowupResponse>(`${this.base}/api/followups/validate`, payload, {
        headers: this.headers(tenantId),
      })
      .pipe(catchError(this.handleError('Valider la relance')));
  }

  getFollowupConversationPreview(
    conversationId?: string,
    tenantId?: string,
  ): Observable<FollowupConversationPreviewResponse> {
    if (!conversationId) {
      this.warnOnce('followups-preview-missing-id');
      this.openInfo('Sélectionnez une conversation pour prévisualiser la relance.');
      return throwError(() => new Error('conversationId required'));
    }

    const params = new HttpParams().set('conversationId', conversationId);
    const headers = this.headers(tenantId);
    const fallback = () =>
      this.http
        .post<FollowupConversationPreviewResponse>(
          `${this.base}/api/followups/preview`,
          { conversationId },
          { headers },
        )
        .pipe(catchError(this.handleError('Prévisualiser la relance')));

    return this.http
      .get<FollowupConversationPreviewResponse>(`${this.base}/api/followups/preview`, {
        params,
        headers,
      })
      .pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status === 400) {
            this.warnOnce('followups-preview-missing-id', error);
            this.openInfo('Sélectionnez une conversation pour prévisualiser la relance.');
            return throwError(() => error);
          }
          if (error.status === 405 || error.status === 415) {
            return fallback();
          }
          return this.handleError('Prévisualiser la relance')(error);
        }),
      );
  }

  hunterSearch(criteria: HunterSearchCriteria, options?: { dryRun?: boolean }): Observable<HunterSearchResponse> {
    let params = new HttpParams();
    if (options?.dryRun) {
      params = params.set('dryRun', 'true');
    }
    return this.http
      .post<HunterSearchResponse>(`${this.base}/api/hunter/search`, criteria, {
        headers: this.headers(),
        params,
      })
      .pipe(
        map((res) => ({
          ...res,
          items: (res.items ?? []).map((item) => ({
            ...item,
            why: item.why ?? [],
          })),
        })),
        catchError(this.handleError('Hunter search')),
      );
  }

  uploadHunterCsv(file: File): Observable<{ csvId: string }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http
      .post<{ csvId: string }>(`${this.base}/api/hunter/upload-csv`, form, {
        headers: this.headers(),
      })
      .pipe(catchError(this.handleError('Upload hunter CSV')));
  }

  seedHunterDemo(): Observable<HunterSearchResponse> {
    return this.http
      .post<HunterSearchResponse>(`${this.base}/api/hunter/seed-demo`, {}, {
        headers: this.headers(),
      })
      .pipe(
        map((res) => ({
          ...res,
          items: (res.items ?? []).map((item) => ({
            ...item,
            why: item.why ?? [],
          })),
        })),
        catchError(this.handleError('Charger les prospects de démo')),
      );
  }

  listSummaries(): Observable<ListSummary[]> {
    return this.http
      .get<ListSummary[]>(`${this.base}/api/lists`, {
        headers: this.headers(),
      })
      .pipe(
        map((res) =>
          Array.isArray(res)
            ? res.map((list) => ({
                ...list,
                name: list.name?.trim() || 'Sans titre',
              }))
            : [],
        ),
        catchError((error: HttpErrorResponse) => {
          if (error.status === 400) {
            this.warnOnce('lists-tenant-missing', error);
            this.openInfo('Sélectionnez un espace de travail / reconnectez-vous.');
            return of([]);
          }
          return this.handleError('Load lists')(error);
        }),
      );
  }

  createList(payload: CreateListPayload): Observable<{ id: string }> {
    return this.http
      .post<{ id: string }>(`${this.base}/api/lists`, payload, {
        headers: this.headers(),
      })
      .pipe(catchError(this.handleError('Create list')));
  }

  renameList(listId: string, payload: CreateListPayload): Observable<void> {
    return this.http
      .put<void>(`${this.base}/api/lists/${listId}`, payload, {
        headers: this.headers(),
      })
      .pipe(catchError(this.handleError('Rename list')));
  }

  deleteList(listId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.base}/api/lists/${listId}`, {
        headers: this.headers(),
      })
      .pipe(catchError(this.handleError('Delete list')));
  }

  addToList(listId: string, payload: AddToListPayload): Observable<AddToListResponse> {
    return this.http
      .post<AddToListResponse>(`${this.base}/api/lists/${listId}/add`, payload, {
        headers: this.headers(),
      })
      .pipe(catchError(this.handleError('Add prospects to list')));
  }

  getList(listId: string): Observable<ProspectListResponse> {
    return this.http
      .get<ProspectListResponse>(`${this.base}/api/lists/${listId}`, {
        headers: this.headers(),
      })
      .pipe(
        map((res) => ({
          ...res,
          items: (res.items ?? []).map((item) => ({
            ...item,
            why: item.why ?? [],
          })),
        })),
        catchError(this.handleError('Load list detail')),
      );
  }

  createCadenceFromList(payload: CadenceFromListPayload): Observable<void> {
    return this.http
      .post<void>(`${this.base}/api/cadences/from-list`, payload, {
        headers: this.headers(),
      })
      .pipe(catchError(this.handleError('Create cadence from list')));
  }

  runDemo(tenantId?: string): Observable<DemoRunResponse> {
    return this.http
      .post<DemoRunResponse>(`${this.base}/api/demo/run`, {}, { headers: this.headers(tenantId) })
      .pipe(catchError(this.handleError('Launch demo')));
  }
}
