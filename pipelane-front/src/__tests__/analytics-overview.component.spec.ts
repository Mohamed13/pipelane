import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { DeliveryAnalyticsResponse, DeliveryTotals } from '../app/core/models';
import { ThemeService } from '../app/core/theme.service';
import { AnalyticsOverviewComponent } from '../app/features/analytics/analytics-overview.component';

class ApiStub {
  getDeliveryAnalytics() {
    return of<DeliveryAnalyticsResponse>({
      totals: DEFAULT_TOTALS,
      byChannel: [],
      byTemplate: [],
      timeline: [],
    });
  }

  getReportSummary() {
    return of({
      from: '',
      to: '',
      totals: DEFAULT_TOTALS,
      byChannel: [],
      topTemplates: [],
      meetingsBooked: 0,
    });
  }

  getTopMessages() {
    return of({
      from: '',
      to: '',
      topByReplies: [],
      topByOpens: [],
    });
  }

  downloadReportSummaryPdf() {
    return of(new Blob());
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
      timeline: [],
    });

    const areaChart = component.areaChart();
    const series = (areaChart?.series as Array<{ name: string; data: number[] }>) || [];
    expect(series[0]!.name).toBe('Sent');
    expect(series[0]!.data).toEqual([10, 10]);
    expect(series[1]!.name).toBe('Delivered');
    expect(series[1]!.data).toEqual([8, 8]);
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
      timeline: [],
    });

    const donut = component.channelDonut();
    expect(donut?.series).toEqual([3, 5]);
    expect(donut?.options?.labels).toEqual(['Email', 'WhatsApp']);
  });

  it('uses top messages replies for bar chart sorted by engagement', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    component['analytics'].set({
      totals: DEFAULT_TOTALS,
      byChannel: [],
      byTemplate: [],
      timeline: [],
    });

    component['topMessages'].set({
      from: '',
      to: '',
      topByReplies: [
        {
          key: 'template:b',
          label: 'Template B',
          channel: 'sms',
          sent: 2,
          delivered: 2,
          opened: 1,
          failed: 0,
          bounced: 0,
          replies: 1,
        },
        {
          key: 'template:a',
          label: 'Template A',
          channel: 'email',
          sent: 3,
          delivered: 3,
          opened: 2,
          failed: 0,
          bounced: 0,
          replies: 3,
        },
      ],
      topByOpens: [],
    });

    const chart = component.templateBar();
    const series = (chart?.series as Array<{ name: string; data: number[] }>) || [];
    expect(series[0]!.name).toBe('Replies');
    expect(series[0]!.data).toEqual([3, 1]);
    expect(chart?.options?.xaxis?.['categories']).toEqual(['Template A', 'Template B']);
  });

  it('falls back to opened ranking when replies are absent', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    component['analytics'].set({
      totals: DEFAULT_TOTALS,
      byChannel: [],
      byTemplate: [],
      timeline: [],
    });

    component['topMessages'].set({
      from: '',
      to: '',
      topByReplies: [
        {
          key: 'template:a',
          label: 'Template A',
          channel: 'email',
          sent: 5,
          delivered: 4,
          opened: 0,
          failed: 0,
          bounced: 0,
          replies: 0,
        },
      ],
      topByOpens: [
        {
          key: 'template:b',
          label: 'Template B',
          channel: 'sms',
          sent: 10,
          delivered: 9,
          opened: 6,
          failed: 1,
          bounced: 0,
          replies: 0,
        },
      ],
    });

    const chart = component.templateBar();
    const series = (chart?.series as Array<{ name: string; data: number[] }>) || [];
    expect(series[0]!.name).toBe('Opened');
    expect(series[0]!.data).toEqual([6]);
    expect(chart?.options?.xaxis?.['categories']).toEqual(['Template B']);
  });

  it('falls back to by-template delivery when no top messages are available', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    component['analytics'].set({
      totals: DEFAULT_TOTALS,
      byChannel: [],
      byTemplate: [
        {
          template: 'Template C',
          channel: 'email',
          queued: 0,
          sent: 4,
          delivered: 4,
          opened: 2,
          failed: 0,
          bounced: 0,
        },
        {
          template: 'Template D',
          channel: 'sms',
          queued: 0,
          sent: 3,
          delivered: 1,
          opened: 0,
          failed: 1,
          bounced: 1,
        },
      ],
      timeline: [],
    });

    component['topMessages'].set({
      from: '',
      to: '',
      topByReplies: [],
      topByOpens: [],
    });

    const chart = component.templateBar();
    const series = (chart?.series as Array<{ name: string; data: number[] }>) || [];
    expect(series[0]!.name).toBe('Delivered');
    expect(series[0]!.data).toEqual([4, 1]);
    expect(chart?.options?.xaxis?.['categories']).toEqual(['Template C', 'Template D']);
  });

  it('uses fallback label when top message is missing label text', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    component['analytics'].set({
      totals: DEFAULT_TOTALS,
      byChannel: [],
      byTemplate: [],
      timeline: [],
    });

    const rawTop = {
      from: '',
      to: '',
      topByReplies: [
        {
          key: 'template:a',
          label: '',
          channel: 'email',
          sent: 5,
          delivered: 4,
          opened: 3,
          failed: 0,
          bounced: 0,
          replies: 2,
        },
      ],
      topByOpens: [],
    };
    const normalized = (
      component as unknown as { normalizeTopMessages: Function }
    ).normalizeTopMessages(rawTop);
    component['topMessages'].set(normalized);

    const chart = component.templateBar();
    const categories = chart?.options?.xaxis?.['categories'] as string[] | undefined;
    expect(categories).toEqual(['(sans libellé)']);
  });

  it('does not throw in ngAfterViewInit when paginator or sort are absent', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    component['paginator'] = undefined;
    component['sort'] = undefined;

    expect(() => TestBed.runInInjectionContext(() => component.ngAfterViewInit())).not.toThrow();
  });

  it('trackKpi falls back to index when item missing', () => {
    const fixture = TestBed.createComponent(AnalyticsOverviewComponent);
    const component = fixture.componentInstance;

    const kpi: Exclude<Parameters<typeof component.trackKpi>[1], null | undefined> = {
      label: 'Test',
      value: 0,
      icon: 'send',
      tooltip: '',
    };
    expect(component.trackKpi(2, kpi)).toBe('Test');
    expect(component.trackKpi(5, undefined)).toBe(5);
  });
});
