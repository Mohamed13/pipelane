import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { AnalyticsOverviewComponent } from '../app/features/analytics/analytics-overview.component';
import { ApiService } from '../app/core/api.service';
import { ThemeService } from '../app/core/theme.service';
import { DeliveryAnalyticsResponse, DeliveryTotals } from '../app/core/models';

class ApiStub {
  getDeliveryAnalytics() {
    return of<DeliveryAnalyticsResponse>({
      totals: DEFAULT_TOTALS,
      byChannel: [],
      byTemplate: [],
    });
  }

  previewFollowups() {
    return of({ count: 0 });
  }
}

class ThemeStub {
  theme = signal<'dark' | 'light'>('dark');
}

const DEFAULT_TOTALS: DeliveryTotals = {
  queued: 0,
  sent: 0,
  delivered: 0,
  opened: 0,
  failed: 0,
  bounced: 0,
};

describe('AnalyticsOverviewComponent mapping', () => {
  beforeAll(() => {
    class MockIntersectionObserver {
      observe() {}
      unobserve() {}
      disconnect() {}
    }
    Object.defineProperty(globalThis, 'IntersectionObserver', {
      configurable: true,
      writable: true,
      value: MockIntersectionObserver,
    });
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AnalyticsOverviewComponent],
      providers: [
        { provide: ApiService, useClass: ApiStub },
        { provide: ThemeService, useClass: ThemeStub },
      ],
    }).compileComponents();
  });

  it('maps analytics totals into area chart series', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    const totals: DeliveryTotals = {
      queued: 2,
      sent: 10,
      delivered: 8,
      opened: 5,
      failed: 1,
      bounced: 1,
    };

    component['timeline'].set([
      { date: new Date('2024-01-01'), totals },
      { date: new Date('2024-01-02'), totals },
    ]);

    component['analytics'].set({
      totals,
      byChannel: [],
      byTemplate: [],
    });

    const areaChart = component.areaChart();
    const series = (areaChart?.series as Array<{ name: string; data: number[] }>) || [];
    expect(series[0].name).toBe('Sent');
    expect(series[0].data).toEqual([10, 10]);
    expect(series[1].name).toBe('Delivered');
    expect(series[1].data).toEqual([8, 8]);
  });

  it('maps channel breakdown to donut chart', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    component['analytics'].set({
      totals: {
        queued: 1,
        sent: 10,
        delivered: 8,
        opened: 5,
        failed: 2,
        bounced: 1,
      },
      byChannel: [
        { channel: 'email', queued: 0, sent: 4, delivered: 3, opened: 1, failed: 1, bounced: 0 },
        { channel: 'whatsapp', queued: 0, sent: 6, delivered: 5, opened: 4, failed: 1, bounced: 1 },
      ],
      byTemplate: [],
    });

    const donut = component.channelDonut();
    expect(donut?.series).toEqual([3, 5]);
    expect(donut?.options?.labels).toEqual(['Email', 'WhatsApp']);
  });
});
