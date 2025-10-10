import { PolicyService } from '../app/core/policy.service';

describe('PolicyService', () => {
  it('allows WA text within 24h', () => {
    const svc = new PolicyService();
    const ok = svc.isWhatsAppTextAllowed(new Date(Date.now() - 3600_000).toISOString());
    expect(ok).toBe(true);
  });
  it('blocks WA text after 24h', () => {
    const svc = new PolicyService();
    const ok = svc.isWhatsAppTextAllowed(new Date(Date.now() - 25 * 3600_000).toISOString());
    expect(ok).toBe(false);
  });
});
