import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PolicyService {
  // Basic client-side rule mirroring the backend: WhatsApp text allowed only if last inbound ≤ 24h
  isWhatsAppTextAllowed(lastInboundUtc?: string | null): boolean {
    if (!lastInboundUtc) return false;
    const last = new Date(lastInboundUtc).getTime();
    return Date.now() - last <= 24 * 3600 * 1000;
  }
}
