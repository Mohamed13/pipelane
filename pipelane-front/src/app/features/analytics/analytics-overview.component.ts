import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatNativeDateModule } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { forkJoin, of } from 'rxjs';
import { catchError, debounceTime, finalize } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import {
  DeliveryAnalyticsResponse,
  DeliveryChannelBreakdown,
  DeliveryTimelinePoint,
  DeliveryTotals,
  ReportSummaryResponse,
  TopMessageItem,
  TopMessagesResponse,
} from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { KpiCardComponent, KpiSparklineConfig } from '../../shared/ui/kpi-card.component';
import { ChartCardComponent, ChartCardConfig } from '../../shared/ui/chart-card.component';
import { RevealOnScrollDirective } from '../../shared/ui/reveal-on-scroll.directive';
import { ApexAxisChartSeries, ApexOptions, ChartType } from 'ng-apexcharts';

type RangePreset = 'today' | '7d' | '30d' | 'custom';

interface DateRange {
  start: Date;
  end: Date;
}

interface TimelinePoint {
  date: Date;
  totals: DeliveryTotals;
}

interface KpiViewModel {
  label: string;
  caption?: string;
  value: number | null;
  valueFormat?: string;
  prefix?: string;
  suffix?: string;
  icon: string;
  tooltip: string;
  delta?: number;
  deltaLabel?: string;
  sparkline?: KpiSparklineConfig;
}

interface ChannelRow {
  channel: string;
  label: string;
  sent: number;
  delivered: number;
  opened: number;
  failed: number;
  bounced: number;
  successRate: number;
  failureRate: number;
}

interface ChartPalette {
  primary: string;
  secondary: string;
  accent: string;
  warn: string;
  text: string;
  textMuted: string;
  surface: string;
  background: string;
  mode: 'dark' | 'light';
}

const DEFAULT_TOTALS: DeliveryTotals = {
  queued: 0,
  sent: 0,
  delivered: 0,
  opened: 0,
  failed: 0,
  bounced: 0,
};

const DAY_MS = 24 * 60 * 60 * 1000;
const RANGE_PRESETS: { key: RangePreset; label: string; tooltip: string }[] = [
  { key: 'today', label: 'Today', tooltip: 'Show events from the last 24 hours' },
  { key: '7d', label: '7 days', tooltip: 'Show the last 7 calendar days' },
  { key: '30d', label: '30 days', tooltip: 'Show the last 30 calendar days' },
  { key: 'custom', label: 'Custom', tooltip: 'Pick a custom date range' },
];

@Component({
  standalone: true,
  selector: 'pl-analytics-overview',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    KpiCardComponent,
    ChartCardComponent,
    RevealOnScrollDirective,
  ],
  templateUrl: './analytics-overview.component.html',
  styleUrls: ['./analytics-overview.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalyticsOverviewComponent implements AfterViewInit {
  private readonly api = inject(ApiService);
  private readonly themeSvc = inject(ThemeService);
  readonly dateRange = new FormGroup({
    start: new FormControl<Date | null>(null),
    end: new FormControl<Date | null>(null),
  });

  readonly rangePreset = signal<RangePreset>('7d');
  readonly selectedRange = signal<DateRange | null>(null);
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly analytics = signal<DeliveryAnalyticsResponse | null>(null);
  readonly previousAnalytics = signal<DeliveryAnalyticsResponse | null>(null);
  readonly timeline = signal<TimelinePoint[]>([]);
  readonly palette = signal<ChartPalette>(buildPalette());
  readonly summary = signal<ReportSummaryResponse | null>(null);
  readonly previousSummary = signal<ReportSummaryResponse | null>(null);
  readonly topMessages = signal<TopMessagesResponse | null>(null);
  readonly exporting = signal(false);

  readonly kpis = computed<KpiViewModel[]>(() => {
    const data = this.analytics();
    const previous = this.previousAnalytics();
    const timeline = this.timeline();
    const summary = this.summary();
    const previousSummary = this.previousSummary();
    if (!data) {
      return [];
    }
    const totals = data.totals ?? DEFAULT_TOTALS;
    const prevTotals = previous?.totals ?? DEFAULT_TOTALS;
    const labels = timeline.map((point) => formatDate(point.date));
    const sentSpark: KpiSparklineConfig = {
      data: timeline.map((p) => p.totals.sent),
      categories: labels,
      color: this.palette().primary,
    };
    const deliveredSpark: KpiSparklineConfig = {
      data: timeline.map((p) => p.totals.delivered),
      categories: labels,
      color: this.palette().accent,
    };
    const openRates = timeline.map((p) => ratio(p.totals.opened, p.totals.delivered) * 100);
    const failRates = timeline.map((p) => failureRate(p.totals) * 100);
    const meetingsCurrent = summary?.meetingsBooked ?? 0;
    const meetingsPrevious = previousSummary?.meetingsBooked ?? 0;

    return [
      {
        label: 'Messages Sent',
        caption: 'Across all channels',
        value: totals.sent,
        icon: 'send',
        tooltip: 'Total outbound messages initiated in the selected period.',
        delta: deltaPercent(totals.sent, prevTotals.sent),
        deltaLabel: 'vs previous period',
        sparkline: sentSpark,
      },
      {
        label: 'Delivered',
        caption: 'Successfully reached recipients',
        value: totals.delivered,
        icon: 'mark_email_read',
        tooltip: 'Messages confirmed as delivered by providers.',
        delta: deltaPercent(totals.delivered, prevTotals.delivered),
        deltaLabel: 'vs previous period',
        sparkline: deliveredSpark,
      },
      {
        label: 'Open Rate',
        caption: 'Avg. engagement',
        value: Math.round(ratio(totals.opened, totals.delivered) * 1000) / 10,
        valueFormat: '1.0-1',
        suffix: '%',
        icon: 'insights',
        tooltip: 'Percentage of delivered messages that were opened.',
        delta: deltaPercent(
          ratio(totals.opened, totals.delivered),
          ratio(prevTotals.opened, prevTotals.delivered),
          true,
        ),
        deltaLabel: 'vs previous',
        sparkline: { data: openRates, categories: labels, color: this.palette().secondary },
      },
      {
        label: 'Failure Rate',
        caption: 'Errors and bounces',
        value: Math.round(failureRate(totals) * 1000) / 10,
        valueFormat: '1.0-1',
        suffix: '%',
        icon: 'warning',
        tooltip: 'Share of messages that failed or bounced.',
        delta: deltaPercent(failureRate(totals), failureRate(prevTotals), true),
        deltaLabel: 'vs previous',
        sparkline: { data: failRates, categories: labels, color: this.palette().warn },
      },
      {
        label: 'Meetings booked',
        caption: 'Replies marked as interested',
        value: meetingsCurrent,
        icon: 'event_available',
        tooltip: 'Replies classified as interested or meeting requested during the selected period.',
        delta: deltaPercent(meetingsCurrent, meetingsPrevious),
        deltaLabel: 'vs previous period',
      },
    ];
  });

  readonly areaChart = computed<ChartCardConfig | null>(() => {
    const data = this.analytics();
    if (!data) {
      return null;
    }
    const timeline = this.timeline();
    if (!timeline.length) {
      return {
        title: 'Delivery timeline',
        subtitle: this.rangeSummary(),
        series: [],
        options: baseChartOptions(this.palette(), 'area'),
        height: 320,
        loading: this.loading(),
        emptyState: {
          title: 'No activity yet',
          message: 'Send a campaign to see delivery performance over time.',
        },
      };
    }
    const palette = this.palette();
    const categories = timeline.map((point) => formatDate(point.date));
    const series = [
      { name: 'Sent', data: timeline.map((point) => point.totals.sent) },
      { name: 'Delivered', data: timeline.map((point) => point.totals.delivered) },
      { name: 'Opened', data: timeline.map((point) => point.totals.opened) },
      { name: 'Failed', data: timeline.map((point) => point.totals.failed + point.totals.bounced) },
    ];

    const baseOptions = baseChartOptions(palette, 'area');
    const options: Partial<ApexOptions> = {
      ...baseOptions,
      chart: {
        ...(baseOptions.chart ?? {}),
        type: 'area',
        toolbar: { show: false },
        animations: { enabled: true },
      },
      colors: [palette.primary, palette.accent, palette.secondary, palette.warn],
      dataLabels: { enabled: false },
      stroke: { curve: 'smooth', width: 3 },
      fill: {
        type: 'gradient',
        gradient: {
          shadeIntensity: 0.6,
          opacityFrom: 0.35,
          opacityTo: 0.05,
        },
      },
      xaxis: {
        categories,
        labels: { style: { colors: categories.map(() => palette.textMuted) } },
      },
      yaxis: [
        {
          labels: { style: { colors: palette.textMuted } },
        },
      ],
      tooltip: {
        theme: palette.mode,
        y: { formatter: (value: number) => formatNumber(value) },
      },
    };

    return {
      title: 'Delivery timeline',
      subtitle: this.rangeSummary(),
      series,
      options,
      height: 360,
      loading: this.loading(),
    };
  });

  readonly channelDonut = computed<ChartCardConfig | null>(() => {
    const data = this.analytics();
    if (!data) {
      return null;
    }
    const palette = this.palette();
    const breakdown = data.byChannel ?? [];

    const series = breakdown.map((channel) => channel.delivered);
    const labels = breakdown.map((channel) => readableChannel(channel.channel));

    const baseOptions = baseChartOptions(palette, 'donut');
    const options: Partial<ApexOptions> = {
      ...baseOptions,
      chart: {
        ...(baseOptions.chart ?? {}),
        type: 'donut',
      },
      legend: {
        position: 'bottom',
        labels: { colors: palette.textMuted },
      },
      dataLabels: { enabled: true },
      tooltip: {
        theme: palette.mode,
        y: { formatter: (value: number) => formatNumber(value) },
      },
      plotOptions: {
        pie: {
          donut: {
            size: '55%',
            background: 'transparent',
            labels: {
              show: true,
              total: {
                show: true,
                label: 'Delivered',
                color: palette.text,
                formatter: () => formatNumber(sum(series)),
              },
            },
          },
        },
      },
      labels,
      colors: [palette.primary, palette.secondary, palette.accent, palette.warn],
    };

    return {
      title: 'Delivery by channel',
      subtitle: 'Delivered messages per provider',
      series,
      options,
      height: 360,
      loading: this.loading(),
      emptyState: {
        title: 'No channel activity',
        message: 'Connect your messaging channels to start tracking performance.',
      },
    };
  });

  readonly templateBar = computed<ChartCardConfig | null>(() => {
    const analytics = this.analytics();
    if (!analytics) {
      return null;
    }
    const palette = this.palette();
    const source = this.rankTopMessages(this.topMessages());

    if (!source.length) {
      const fallback = [...(analytics.byTemplate ?? [])].sort((a, b) => b.delivered - a.delivered).slice(0, 6);
      if (!fallback.length) {
        return {
          title: 'Top messages',
          subtitle: 'Replies and opens by template',
          series: [],
          options: baseChartOptions(palette, 'bar'),
          height: 360,
          loading: this.loading(),
          emptyState: {
            title: 'No message activity',
            message: 'Send a campaign to view engagement insights.',
          },
        };
      }

      const labels = fallback.map((template) => template.template ?? 'Untitled');
      const data = fallback.map((template) => template.delivered);
      const baseOptions = baseChartOptions(palette, 'bar');
      const options: Partial<ApexOptions> = {
        ...baseOptions,
        chart: {
          ...(baseOptions.chart ?? {}),
          type: 'bar',
        },
        plotOptions: {
          bar: {
            horizontal: true,
            borderRadius: 8,
            barHeight: '60%',
          },
        },
        dataLabels: { enabled: false },
        xaxis: {
          categories: labels,
          labels: { style: { colors: labels.map(() => palette.textMuted) } },
        },
        colors: [palette.accent],
        tooltip: { theme: palette.mode, y: { formatter: (value: number) => formatNumber(value) } },
      };

      return {
        title: 'Top templates',
        subtitle: 'Delivered messages per template',
        series: [{ name: 'Delivered', data }],
        options,
        height: 360,
        loading: this.loading(),
      };
    }

    const labels = source.map((item) => item.label || 'Untitled');
    const replies = source.map((item) => item.replies);
    const opens = source.map((item) => item.opened);
    const delivered = source.map((item) => item.delivered);

    const hasReplies = replies.some((value) => value > 0);
    const hasOpens = opens.some((value) => value > 0);

    const series: ApexAxisChartSeries = [];
    if (hasReplies) {
      series.push({ name: 'Replies', data: replies });
    }
    if (hasOpens) {
      series.push({ name: 'Opened', data: opens });
    }
    if (!series.length) {
      series.push({ name: 'Delivered', data: delivered });
    }

    const baseOptions = baseChartOptions(palette, 'bar');
    const options: Partial<ApexOptions> = {
      ...baseOptions,
      chart: {
        ...(baseOptions.chart ?? {}),
        type: 'bar',
        stacked: series.length > 1,
      },
      plotOptions: {
        bar: {
          horizontal: true,
          borderRadius: 8,
          barHeight: '60%',
        },
      },
      dataLabels: { enabled: false },
      xaxis: {
        categories: labels,
        labels: { style: { colors: labels.map(() => palette.textMuted) } },
      },
      colors: series.length > 1
        ? [palette.accent, palette.primary, palette.secondary]
        : [palette.primary],
      tooltip: {
        theme: palette.mode,
        y: { formatter: (value: number) => formatNumber(value) },
      },
    };

    return {
      title: 'Top-performing messages',
      subtitle: hasReplies ? 'Replies and opens for the last period' : 'Open and delivery counts',
      series,
      options,
      height: 360,
      loading: this.loading(),
      emptyState: {
        title: 'No engagement yet',
        message: 'Send a nudge to gather some replies.',
      },
    };
  });

  readonly rangeSummary = computed(() => {
    const range = this.selectedRange();
    if (!range) {
      return '';
    }
    return `${formatDate(range.start)} – ${formatDate(range.end)}`;
  });

  readonly table = new MatTableDataSource<ChannelRow>([]);
  readonly displayedColumns = ['channel', 'sent', 'delivered', 'opened', 'failed', 'successRate'];

  @ViewChild(MatPaginator) paginator?: MatPaginator;
  @ViewChild(MatSort) sort?: MatSort;

  constructor() {
    effect(() => {
      this.themeSvc.theme();
      this.palette.set(buildPalette());
    });

    this.dateRange.valueChanges.pipe(debounceTime(120), takeUntilDestroyed()).subscribe((value) => {
      if (!value) {
        return;
      }
      const { start, end } = value;
      if (start && end) {
        this.rangePreset.set('custom');
        const ordered = orderRange(start, end);
        this.selectedRange.set(ordered);
        this.loadRange(ordered);
      }
    });

    this.loadRange(computePresetRange('7d'));
  }

  ngAfterViewInit(): void {
    if (this.paginator) {
      this.table.paginator = this.paginator;
    }
    if (this.sort) {
      this.table.sort = this.sort;
    }

    effect(() => {
      const breakdown = this.analytics()?.byChannel ?? [];
      this.table.data = breakdown.map(mapChannelRow);
      if (this.table.paginator) {
        this.table.paginator.firstPage();
      }
    });
  }

  presetItems = RANGE_PRESETS;

  selectPreset(preset: RangePreset): void {
    this.rangePreset.set(preset);
    if (preset === 'custom') {
      return;
    }
    this.dateRange.setValue({ start: null, end: null }, { emitEvent: false });
    const range = computePresetRange(preset);
    this.selectedRange.set(range);
    this.loadRange(range);
  }

  retry(): void {
    const range = this.selectedRange() ?? computePresetRange(this.rangePreset());
    this.loadRange(range);
  }

  downloadSummary(): void {
    if (this.loading() || this.exporting()) {
      return;
    }
    const range = this.selectedRange() ?? computePresetRange(this.rangePreset());
    const normalized = normaliseRange(range);
    const fromIso = toIso(startOfDay(normalized.start));
    const toIsoValue = toIso(endOfDay(normalized.end));

    if (typeof window === 'undefined') {
      return;
    }

    this.exporting.set(true);
    this.api
      .downloadReportSummaryPdf(fromIso, toIsoValue)
      .pipe(
        takeUntilDestroyed(),
        finalize(() => this.exporting.set(false)),
      )
      .subscribe({
        next: (blob) => {
          const fromSlug = normalized.start.toISOString().slice(0, 10);
          const toSlug = normalized.end.toISOString().slice(0, 10);
          const url = URL.createObjectURL(blob);
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = `pipelane-summary-${fromSlug}-${toSlug}.pdf`;
          anchor.click();
          URL.revokeObjectURL(url);
        },
        error: () => {
          // handled by handleError snackbar
        },
      });
  }

  private loadRange(range: DateRange): void {
    this.loading.set(true);
    this.error.set(null);
    this.topMessages.set(null);
    const currentRange = normaliseRange(range);
    const durationDays = Math.max(
      1,
      Math.round((currentRange.end.getTime() - currentRange.start.getTime()) / DAY_MS) + 1,
    );
    const previousRange: DateRange = {
      start: addDays(currentRange.start, -durationDays),
      end: addDays(currentRange.start, -1),
    };

    const currentStartIso = toIso(startOfDay(currentRange.start));
    const currentEndIso = toIso(endOfDay(currentRange.end));
    const previousStartIso = toIso(startOfDay(previousRange.start));
    const previousEndIso = toIso(endOfDay(previousRange.end));

    forkJoin({
      current: this.api.getDeliveryAnalytics(currentStartIso, currentEndIso),
      previous: this.api.getDeliveryAnalytics(previousStartIso, previousEndIso),
      summary: this.api.getReportSummary(currentStartIso, currentEndIso),
      previousSummary: this.api.getReportSummary(previousStartIso, previousEndIso),
      topMessages: this.api.getTopMessages(currentStartIso, currentEndIso),
    })
      .pipe(
        catchError((error: unknown) => {
          this.error.set('Unable to load analytics right now. Please retry in a minute.');
          console.error('Analytics load failed', error);
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (!result) {
          this.analytics.set(null);
          this.previousAnalytics.set(null);
          this.summary.set(null);
          this.previousSummary.set(null);
          this.timeline.set([]);
           this.topMessages.set(null);
          this.loading.set(false);
          return;
        }
        this.analytics.set(result.current);
        this.previousAnalytics.set(result.previous);
        this.summary.set(result.summary);
        this.previousSummary.set(result.previousSummary);
        this.timeline.set(this.mapTimeline(result.current.timeline ?? []));
        this.topMessages.set(this.normalizeTopMessages(result.topMessages));
        this.selectedRange.set(range);
        this.loading.set(false);
      });
  }

  private mapTimeline(points: DeliveryTimelinePoint[]): TimelinePoint[] {
    if (!points.length) {
      return [];
    }
    return points
      .slice()
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
      .map((point) => ({
        date: new Date(point.date),
        totals: {
          queued: point.queued ?? 0,
          sent: point.sent ?? 0,
          delivered: point.delivered ?? 0,
          opened: point.opened ?? 0,
          failed: point.failed ?? 0,
          bounced: point.bounced ?? 0,
        },
      }));
  }

  private normalizeTopMessages(top: TopMessagesResponse | null | undefined): TopMessagesResponse | null {
    if (!top) {
      return null;
    }
    return {
      ...top,
      topByReplies: [...(top.topByReplies ?? [])],
      topByOpens: [...(top.topByOpens ?? [])],
    };
  }

  private rankTopMessages(top: TopMessagesResponse | null): TopMessageItem[] {
    if (!top) {
      return [];
    }
    const replies = [...(top.topByReplies ?? [])].sort((a, b) => {
      const replyDelta = b.replies - a.replies;
      if (replyDelta !== 0) {
        return replyDelta;
      }
      const openDelta = b.opened - a.opened;
      if (openDelta !== 0) {
        return openDelta;
      }
      return b.delivered - a.delivered;
    });
    const repliesWithActivity = replies.filter((item) => item.replies > 0);
    if (repliesWithActivity.length) {
      return repliesWithActivity.slice(0, 6);
    }

    const opens = [...(top.topByOpens ?? [])].sort((a, b) => {
      const openDelta = b.opened - a.opened;
      if (openDelta !== 0) {
        return openDelta;
      }
      return b.delivered - a.delivered;
    });
    const opensWithActivity = opens.filter((item) => item.opened > 0);
    if (opensWithActivity.length) {
      return opensWithActivity.slice(0, 6);
    }

    return [];
  }
}

function computePresetRange(preset: RangePreset): DateRange {
  const today = new Date();
  switch (preset) {
    case 'today': {
      return { start: startOfDay(today), end: endOfDay(today) };
    }
    case '7d': {
      const end = endOfDay(today);
      const start = addDays(startOfDay(today), -6);
      return { start, end };
    }
    case '30d': {
      const end = endOfDay(today);
      const start = addDays(startOfDay(today), -29);
      return { start, end };
    }
    default:
      return { start: startOfDay(today), end: endOfDay(today) };
  }
}

function normaliseRange(range: DateRange): DateRange {
  return {
    start: startOfDay(range.start),
    end: endOfDay(range.end),
  };
}

function orderRange(start: Date, end: Date): DateRange {
  if (start <= end) {
    return { start: startOfDay(start), end: endOfDay(end) };
  }
  return { start: startOfDay(end), end: endOfDay(start) };
}

function startOfDay(date: Date): Date {
  const copy = new Date(date);
  copy.setHours(0, 0, 0, 0);
  return copy;
}

function endOfDay(date: Date): Date {
  const copy = new Date(date);
  copy.setHours(23, 59, 59, 999);
  return copy;
}

function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

function toIso(date: Date): string {
  return new Date(date).toISOString();
}

function formatDate(date: Date): string {
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' }).format(date);
}

function ratio(numerator: number, denominator: number): number {
  if (!denominator) {
    return 0;
  }
  return numerator / denominator;
}

function failureRate(totals: DeliveryTotals): number {
  const failed = totals.failed + totals.bounced;
  return ratio(failed, totals.sent);
}

function deltaPercent(current: number, previous: number, isRatio = false): number | undefined {
  if (isRatio) {
    if (!isFinite(current) || !isFinite(previous)) {
      return undefined;
    }
  }
  if (!previous) {
    return undefined;
  }
  const delta = ((current - previous) / Math.abs(previous)) * 100;
  if (!isFinite(delta)) {
    return undefined;
  }
  return Math.round(delta * 10) / 10;
}

function sum(values: number[]): number {
  return values.reduce((acc, value) => acc + value, 0);
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

function readableChannel(channel: string): string {
  switch (channel) {
    case 'whatsapp':
      return 'WhatsApp';
    case 'email':
      return 'Email';
    case 'sms':
      return 'SMS';
    default:
      return channel.toUpperCase();
  }
}

function mapChannelRow(entry: DeliveryChannelBreakdown): ChannelRow {
  const failure = entry.failed + entry.bounced;
  const successRate = ratio(entry.delivered, entry.sent) * 100;
  const failureRateValue = ratio(failure, entry.sent) * 100;
  return {
    channel: entry.channel,
    label: readableChannel(entry.channel),
    sent: entry.sent,
    delivered: entry.delivered,
    opened: entry.opened,
    failed: entry.failed,
    bounced: entry.bounced,
    successRate,
    failureRate: failureRateValue,
  };
}

function baseChartOptions(palette: ChartPalette, chartType: ChartType): Partial<ApexOptions> {
  return {
    chart: {
      background: 'transparent',
      type: chartType,
    },
    theme: { mode: palette.mode },
    grid: {
      show: true,
      borderColor: palette.mode === 'dark' ? 'rgba(255,255,255,0.05)' : 'rgba(15,23,42,0.08)',
    },
  };
}

function buildPalette(): ChartPalette {
  if (typeof window === 'undefined') {
    return {
      primary: '#75F0FF',
      secondary: '#9B8CFF',
      accent: '#60F7A3',
      warn: '#FF6584',
      text: '#E6EAF2',
      textMuted: '#A6B0C3',
      surface: '#101726',
      background: '#0b0f17',
      mode: 'dark',
    };
  }
  const root = getComputedStyle(document.documentElement);
  return {
    primary: root.getPropertyValue('--color-primary').trim() || '#75F0FF',
    secondary: root.getPropertyValue('--color-secondary').trim() || '#9B8CFF',
    accent: root.getPropertyValue('--color-accent').trim() || '#60F7A3',
    warn: '#FF6584',
    text: root.getPropertyValue('--color-text').trim() || '#E6EAF2',
    textMuted: root.getPropertyValue('--color-text-muted').trim() || '#A6B0C3',
    surface: root.getPropertyValue('--color-surface').trim() || '#101726',
    background: root.getPropertyValue('--color-bg').trim() || '#0b0f17',
    mode: document.documentElement.classList.contains('theme-light') ? 'light' : 'dark',
  };
}
