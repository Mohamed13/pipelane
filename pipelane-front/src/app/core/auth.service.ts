import { Injectable, computed, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from './environment';

type LoginResponse = { token: string; tenantId: string; role: string };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private storageKey = 'pl_token';
  token = signal<string | null>(localStorage.getItem(this.storageKey));
  tenantId = computed(() => this.decodeTid(this.token()));

  constructor() {}

  login(email: string, password: string, tenantId?: string) {
    return this.http.post<LoginResponse>(`${environment.API_BASE_URL}/auth/login`, { email, password, tenantId }).subscribe(res => {
      this.token.set(res.token);
      localStorage.setItem(this.storageKey, res.token);
    });
  }

  logout() {
    this.token.set(null);
    localStorage.removeItem(this.storageKey);
  }

  private decodeTid(token: string | null): string | null {
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['tid'] ?? null;
    } catch { return null; }
  }
}
