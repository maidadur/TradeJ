import { Component, ElementRef, Input, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import {
  CandlestickSeries,
  ColorType,
  IChartApi,
  IPriceLine,
  ISeriesApi,
  LineStyle,
  UTCTimestamp,
  createChart
} from 'lightweight-charts';
import { Trade } from '../../../../core/models/trade.model';
import { Bar, Timeframe, TIMEFRAMES } from '../../../../core/models/bar.model';
import { BarsService } from '../../../../core/services/bars.service';

// Minimum window (ms) to pad around entry/exit per timeframe so there's context on both sides.
const MIN_PADDING_MS: Record<Timeframe, number> = {
  M1:  30 * 60_000,
  M5:  2 * 3_600_000,
  M15: 6 * 3_600_000,
  M30: 12 * 3_600_000,
  H1:  24 * 3_600_000,
  H4:  4 * 86_400_000,
  D1:  14 * 86_400_000
};

function defaultTimeframe(durationMs: number): Timeframe {
  if (durationMs < 2 * 3_600_000) return 'M1';
  if (durationMs < 86_400_000) return 'M5';
  if (durationMs < 5 * 86_400_000) return 'H1';
  return 'D1';
}

@Component({
  selector: 'app-trade-chart',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectModule],
  templateUrl: './trade-chart.component.html',
  styleUrl: './trade-chart.component.scss'
})
export class TradeChartComponent implements OnInit, OnDestroy {
  @Input({ required: true }) trade!: Trade;
  @ViewChild('container', { static: true }) containerRef!: ElementRef<HTMLDivElement>;
  @ViewChild('zoneTp', { static: true }) zoneTpRef!: ElementRef<HTMLDivElement>;
  @ViewChild('zoneSl', { static: true }) zoneSlRef!: ElementRef<HTMLDivElement>;

  private barsService = inject(BarsService);
  private chart: IChartApi | null = null;
  private series: ISeriesApi<'Candlestick'> | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private priceLines: IPriceLine[] = [];
  private zoneEntrySec: UTCTimestamp | null = null;
  private zoneEndSec: UTCTimestamp | null = null;

  timeframeOptions = TIMEFRAMES.map(tf => ({ label: tf, value: tf }));
  timeframe = signal<Timeframe>('M5');
  loading = signal(false);
  error = signal<string | null>(null);
  hasBars = signal(true);

  ngOnInit(): void {
    this.timeframe.set(defaultTimeframe(this.durationMs()));
    this.initChart();
    this.loadBars();

    this.resizeObserver = new ResizeObserver(() => {
      const el = this.containerRef.nativeElement;
      this.chart?.resize(el.clientWidth, el.clientHeight);
      this.updateZones();
    });
    this.resizeObserver.observe(this.containerRef.nativeElement);
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.chart?.remove();
  }

  onTimeframeChange(tf: Timeframe): void {
    this.timeframe.set(tf);
    this.loadBars();
  }

  // Backend timestamps are UTC instants but arrive without a zone suffix (EF Core/SQLite drops
  // DateTime.Kind on round-trip) — the JS Date parser treats a zone-less string as local time,
  // which silently shifts it by the viewer's UTC offset. Force UTC parsing here.
  private toUtcMs(iso: string): number {
    const hasZone = /[Zz]|[+-]\d{2}:\d{2}$/.test(iso);
    return new Date(hasZone ? iso : iso + 'Z').getTime();
  }

  private durationMs(): number {
    const entry = this.toUtcMs(this.trade.entryTime);
    const exit = this.trade.exitTime ? this.toUtcMs(this.trade.exitTime) : Date.now();
    return Math.max(exit - entry, 60_000);
  }

  private range(): { from: string; to: string } {
    const entry = this.toUtcMs(this.trade.entryTime);
    const exit = this.trade.exitTime ? this.toUtcMs(this.trade.exitTime) : Date.now();
    const padding = Math.max((exit - entry) * 0.2, MIN_PADDING_MS[this.timeframe()]);
    return {
      from: new Date(entry - padding).toISOString(),
      to: new Date(exit + padding).toISOString()
    };
  }

  private initChart(): void {
    const el = this.containerRef.nativeElement;
    this.chart = createChart(el, {
      width: el.clientWidth,
      height: el.clientHeight,
      layout: {
        background: { type: ColorType.Solid, color: '#12151f' },
        textColor: '#64748b'
      },
      grid: {
        vertLines: { color: '#1e2235' },
        horzLines: { color: '#1e2235' }
      },
      timeScale: { borderColor: '#2d3148', timeVisible: true, secondsVisible: false },
      rightPriceScale: { borderColor: '#2d3148' },
      crosshair: { mode: 0 }
    });

    this.series = this.chart.addSeries(CandlestickSeries, {
      upColor: '#4ade80',
      downColor: '#f87171',
      borderVisible: false,
      wickUpColor: '#4ade80',
      wickDownColor: '#f87171'
    });

    this.chart.timeScale().subscribeVisibleTimeRangeChange(() => this.updateZones());
  }

  private loadBars(): void {
    if (!this.series) return;
    this.loading.set(true);
    this.error.set(null);

    const { from, to } = this.range();
    this.barsService.getBars(this.trade.accountId, this.trade.symbol, this.timeframe(), from, to).subscribe({
      next: bars => {
        this.loading.set(false);
        this.hasBars.set(bars.length > 0);
        if (!this.series) return;

        this.series.setData(bars.map(b => ({
          time: b.time as UTCTimestamp,
          open: b.open,
          high: b.high,
          low: b.low,
          close: b.close
        })));

        this.drawPriceLines();
        this.setZoneRange(bars);
        this.chart?.timeScale().fitContent();
        this.updateZones();
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? 'Could not load chart data.');
      }
    });
  }

  private drawPriceLines(): void {
    if (!this.series) return;

    for (const line of this.priceLines) this.series.removePriceLine(line);
    this.priceLines = [];

    this.priceLines.push(this.series.createPriceLine({
      price: this.trade.entryPrice,
      color: '#6366f1',
      lineWidth: 1,
      lineStyle: LineStyle.Solid,
      axisLabelVisible: true,
      title: 'Entry'
    }));

    if (this.trade.exitPrice != null) {
      this.priceLines.push(this.series.createPriceLine({
        price: this.trade.exitPrice,
        color: '#94a3b8',
        lineWidth: 1,
        lineStyle: LineStyle.Dashed,
        axisLabelVisible: true,
        title: 'Exit'
      }));
    }

    if (this.trade.stopLoss != null) {
      this.priceLines.push(this.series.createPriceLine({
        price: this.trade.stopLoss,
        color: '#f87171',
        lineWidth: 1,
        lineStyle: LineStyle.Dashed,
        axisLabelVisible: true,
        title: 'SL'
      }));
    }

    if (this.trade.takeProfit != null) {
      this.priceLines.push(this.series.createPriceLine({
        price: this.trade.takeProfit,
        color: '#4ade80',
        lineWidth: 1,
        lineStyle: LineStyle.Dashed,
        axisLabelVisible: true,
        title: 'TP'
      }));
    }
  }

  private setZoneRange(bars: Bar[]): void {
    const entryMs = this.toUtcMs(this.trade.entryTime);
    const lastBarMs = bars.length > 0 ? bars[bars.length - 1].time * 1000 : Date.now();
    const endMs = this.trade.exitTime ? this.toUtcMs(this.trade.exitTime) : lastBarMs;
    this.zoneEntrySec = Math.floor(entryMs / 1000) as UTCTimestamp;
    this.zoneEndSec = Math.floor(endMs / 1000) as UTCTimestamp;
  }

  // Renders the TP/SL profit-loss bands as plain overlay divs (positioned via the chart's own
  // coordinate functions) rather than a canvas primitive — simpler and far easier to keep correct.
  private updateZones(): void {
    const tpEl = this.zoneTpRef?.nativeElement;
    const slEl = this.zoneSlRef?.nativeElement;
    if (!tpEl || !slEl) return;

    if (!this.chart || !this.series || this.zoneEntrySec == null || this.zoneEndSec == null) {
      tpEl.hidden = true;
      slEl.hidden = true;
      return;
    }

    const timeScale = this.chart.timeScale();
    const x1 = timeScale.timeToCoordinate(this.zoneEntrySec);
    const x2 = timeScale.timeToCoordinate(this.zoneEndSec);
    const yEntry = this.series.priceToCoordinate(this.trade.entryPrice);

    if (x1 === null || x2 === null || yEntry === null) {
      tpEl.hidden = true;
      slEl.hidden = true;
      return;
    }

    const left = Math.min(x1, x2);
    const width = Math.abs(x2 - x1);

    this.positionZone(tpEl, this.trade.takeProfit, yEntry, left, width);
    this.positionZone(slEl, this.trade.stopLoss, yEntry, left, width);
  }

  private positionZone(el: HTMLDivElement, price: number | null | undefined, yEntry: number, left: number, width: number): void {
    if (price == null || !this.series) {
      el.hidden = true;
      return;
    }
    const y = this.series.priceToCoordinate(price);
    if (y === null) {
      el.hidden = true;
      return;
    }
    el.hidden = false;
    el.style.left = `${left}px`;
    el.style.width = `${width}px`;
    el.style.top = `${Math.min(y, yEntry)}px`;
    el.style.height = `${Math.abs(y - yEntry)}px`;
  }
}
