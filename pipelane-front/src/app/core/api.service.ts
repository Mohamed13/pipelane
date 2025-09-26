import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from './environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.API_BASE_URL;
  private headers(tenantId?: string) {
    return tenantId ? { 'X-Tenant-Id': tenantId } : {};
  }

  saveChannelSettings(body: any, tenantId?: string) {
    return this.http.post(`${this.base}/onboarding/channel-settings`, body, { headers: this.headers(tenantId) });
  }
  getTemplates(tenantId?: string) {
    return this.http.get(`${this.base}/templates`, { headers: this.headers(tenantId) });
  }
  refreshTemplates(tenantId?: string) {
    return this.http.post(`${this.base}/templates/refresh`, {}, { headers: this.headers(tenantId) });
  }
  searchContacts(q: string, page = 1, size = 20, tenantId?: string) {
    const params = new HttpParams().set('search', q).set('page', page).set('size', size);
    return this.http.get(`${this.base}/contacts`, { params, headers: this.headers(tenantId) });
  }
  getConversation(contactId: string, last = 50, tenantId?: string) {
    const params = new HttpParams().set('last', last);
    return this.http.get(`${this.base}/conversations/${contactId}`, { params, headers: this.headers(tenantId) });
  }
  sendMessage(body: any, tenantId?: string) {
    return this.http.post(`${this.base}/messages/send`, body, { headers: this.headers(tenantId) });
  }
  createCampaign(body: any, tenantId?: string) {
    return this.http.post(`${this.base}/campaigns`, body, { headers: this.headers(tenantId) });
  }
  getCampaign(id: string, tenantId?: string) {
    return this.http.get(`${this.base}/campaigns/${id}`, { headers: this.headers(tenantId) });
  }
  getAnalyticsOverview(from?: string, to?: string, tenantId?: string) {
    const params = new HttpParams({ fromObject: { from: from ?? '', to: to ?? '' } });
    return this.http.get(`${this.base}/analytics/overview`, { params, headers: this.headers(tenantId) });
  }
}

