import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal, inject } from '@angular/core';
import { Router } from '@angular/router';

import { environment } from './environment';

type LoginResponse = { token: string; tenantId: string; role: string };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private storageKey = 'pl_token';
  token = signal<string | null>(localStorage.getItem(this.storageKey));
  tenantId = computed(() => this.decodeTid(this.token()));

  constructor() {}

  login(email: string, password: string, tenantId?: string) {
    return this.http
      .post<LoginResponse>(`${environment.API_BASE_URL}/auth/login`, { email, password, tenantId })
      .subscribe({
        next: (res) => {
          this.token.set(res.token);
          localStorage.setItem(this.storageKey, res.token);
          // redirect to analytics on successful login
          this.router.navigateByUrl('/analytics');
        },
        error: () => {
          // no-op minimal; UI can add feedback later
        },
      });
  }

  logout() {
    this.token.set(null);
    localStorage.removeItem(this.storageKey);
    this.router.navigateByUrl('/login');
  }

  private decodeTid(token: string | null): string | null {
    if (!token) return null;
    try {
      const segments = token.split('.');
      if (segments.length < 2 || !segments[1]) {
        return null;
      }
      const payload = JSON.parse(atob(segments[1]));
      return payload['tid'] ?? null;
    } catch {
      return null;
    }
  }
}
