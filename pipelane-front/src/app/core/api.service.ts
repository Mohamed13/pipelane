import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AuthService } from './auth.service';
import { environment } from './environment';
import {
  CampaignCreatePayload,
  CampaignDetail,
  ChannelSettingsPayload,
  ConversationResponse,
  DeliveryAnalyticsResponse,
  FollowupPreviewResponse,
  PagedContactsResponse,
  SendMessageRequestPayload,
  SendMessageResponse,
  TemplateSummary,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly snackbar = inject(MatSnackBar, { optional: true });
  private readonly base = environment.API_BASE_URL;

  private headers(explicitTenantId?: string): Record<string, string> {
    const tenantId = explicitTenantId ?? this.auth.tenantId();
    return tenantId ? { 'X-Tenant-Id': tenantId } : {};
  }

  private handleError(context: string) {
    return (error: HttpErrorResponse) => {
      const detail = this.extractErrorMessage(error);
      this.snackbar?.open(`${context}: ${detail}`, 'Dismiss', { duration: 6000 });
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
      .pipe(catchError(this.handleError('Loading delivery analytics')));
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
}
