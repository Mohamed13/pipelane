import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';

import { environment } from './environment';

type LoginResponse = { token: string; tenantId: string; role: string; expiresIn?: number };
type TokenClaims = Record<string, unknown>;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly storageKey = 'pl_token';
  readonly token = signal<string | null>(this.loadInitialToken());
  private readonly claimsSignal = signal<TokenClaims | null>(this.decodeClaims(this.token()));
  readonly claims = computed(() => this.claimsSignal());
  readonly tenantId = computed(() => {
    const claims = this.claimsSignal();
    const tid = claims?.['tid'];
    return typeof tid === 'string' ? tid : null;
  });

  login(email: string, password: string, remember = false, tenantId?: string) {
    return this.http
      .post<LoginResponse>(`${environment.API_BASE_URL}/auth/login`, { email, password, tenantId })
      .pipe(tap((res) => this.persistToken(res.token, remember)));
  }

  logout(redirectTo?: string | null) {
    const target = redirectTo ?? this.router.url;
    this.clearToken();
    if (target && !target.startsWith('/login')) {
      this.router.navigate(['/login'], { queryParams: { redirect: target } });
    } else {
      this.router.navigate(['/login']);
    }
  }

  private persistToken(token: string, remember: boolean) {
    this.token.set(token);
    this.claimsSignal.set(this.decodeClaims(token));
    try {
      sessionStorage.removeItem(this.storageKey);
    } catch {
      /* no-op */
    }
    try {
      localStorage.removeItem(this.storageKey);
    } catch {
      /* no-op */
    }
    const storage = remember ? safeStorage('localStorage') : safeStorage('sessionStorage');
    storage?.setItem(this.storageKey, token);
  }

  private clearToken() {
    this.token.set(null);
    this.claimsSignal.set(null);
    try {
      sessionStorage.removeItem(this.storageKey);
    } catch {
      /* no-op */
    }
    try {
      localStorage.removeItem(this.storageKey);
    } catch {
      /* no-op */
    }
  }

  private loadInitialToken(): string | null {
    const sessionToken = safeStorage('sessionStorage')?.getItem(this.storageKey);
    if (sessionToken) {
      return sessionToken;
    }
    return safeStorage('localStorage')?.getItem(this.storageKey) ?? null;
  }

  private decodeClaims(token: string | null): TokenClaims | null {
    if (!token) {
      return null;
    }
    try {
      const [, payload] = token.split('.');
      if (!payload) {
        return null;
      }
      const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
      const json = atob(normalized);
      return JSON.parse(json) as TokenClaims;
    } catch {
      return null;
    }
  }
}

function safeStorage(type: 'localStorage' | 'sessionStorage'): Storage | null {
  try {
    const scope = globalThis as unknown as Record<
      'localStorage' | 'sessionStorage',
      Storage | undefined
    >;
    return scope?.[type] ?? null;
  } catch {
    return null;
  }
}
