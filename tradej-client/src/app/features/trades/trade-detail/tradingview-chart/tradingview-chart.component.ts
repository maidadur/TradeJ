import {
  Component, Input, AfterViewInit, OnChanges, OnDestroy,
  SimpleChanges, ViewChild, ElementRef, ChangeDetectionStrategy, NgZone, inject
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  createChart, CandlestickSeries, ColorType, LineStyle,
  IChartApi, ISeriesApi, CandlestickData, UTCTimestamp, createSeriesMarkers,
  IPriceLine, ISeriesMarkersPluginApi
} from 'lightweight-charts';

@Component({
  selector: 'app-tradingview-chart',
  standalone: true,
  template: `
    <div class="tv-wrapper">
      <div class="tv-header">
        <span class="tv-symbol">{{ symbol }}</span>
        <span class="tv-meta">
          {{ directionLabel }} &nbsp;·&nbsp;
          Entry <strong>{{ entryPrice | number:'1.2-8' }}</strong>
          @if (exitPrice != null) {
            &nbsp;→&nbsp; Exit <strong>{{ exitPrice | number:'1.2-8' }}</strong>
          }
        </span>
        <div class="tv-tf-bar">
          @for (tf of timeframes; track tf.label) {
            <button
              class="tv-tf-btn"
              [class.active]="activeTimeframe === tf.label"
              (click)="setTimeframe(tf)"
            >{{ tf.label }}</button>
          }
        </div>
        @if (error) {
          <span class="tv-error">{{ error }}</span>
        }
      </div>
      <div class="tv-canvas" #canvas></div>
      <div class="tv-footer">
        <a href="https://www.tradingview.com/lightweight-charts/" target="_blank" rel="noopener nofollow">
          Lightweight Charts™ by TradingView
        </a>
      </div>
    </div>
  `,
  imports: [DecimalPipe],
  styleUrl: './tradingview-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TradingviewChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() symbol!: string;
  @Input() entryTime!: string;
  @Input() exitTime?: string;
  @Input() entryPrice!: number;
  @Input() exitPrice?: number;
  @Input() direction!: 'Long' | 'Short';

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLDivElement>;

  private http = inject(HttpClient);
  private zone = inject(NgZone);

  private chart?: IChartApi;
  private series?: ISeriesApi<'Candlestick'>;
  private entryLine?: IPriceLine;
  private exitLine?: IPriceLine;
  private markersPlugin?: ISeriesMarkersPluginApi<any>;
  private timestampMap: number[] = [];
  private viewReady = false;
  error = '';

  readonly timeframes = [
    { label: '1m',  interval: '1m',  range: '1d'  },
    { label: '5m',  interval: '5m',  range: '5d'  },
    { label: '15m', interval: '15m', range: '5d'  },
    { label: '1h',  interval: '1h',  range: '1mo' },
    { label: '4h',  interval: '60m', range: '1mo' },
    { label: '1D',  interval: '1d',  range: '3mo' },
    { label: '1W',  interval: '1wk', range: '1y'  },
  ];
  activeTimeframe = '';
  private overrideInterval?: string;
  private overrideRange?: string;

  get directionLabel(): string {
    return this.direction === 'Long' ? '▲ Long' : '▼ Short';
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.zone.runOutsideAngular(() => this.buildChart());
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (this.viewReady && (changes['symbol'] || changes['entryTime'] || changes['exitTime'])) {
      this.overrideInterval = undefined;
      this.overrideRange = undefined;
      this.activeTimeframe = '';
      this.destroyChart();
      this.zone.runOutsideAngular(() => this.buildChart());
    }
  }

  setTimeframe(tf: { label: string; interval: string; range: string }): void {
    this.activeTimeframe = tf.label;
    this.overrideInterval = tf.interval;
    this.overrideRange = tf.range;
    this.error = '';
    this.series?.setData([]);
    this.zone.runOutsideAngular(() => this.fetchAndRender());
  }

  ngOnDestroy(): void {
    this.destroyChart();
  }

  private destroyChart(): void {
    this.clearAnnotations();
    this.chart?.remove();
    this.chart = undefined;
    this.series = undefined;
  }

  private buildChart(): void {
    const el = this.canvasRef?.nativeElement;
    if (!el) return;

    this.chart = createChart(el, {
      layout: {
        background: { type: ColorType.Solid, color: '#0d0f17' },
        textColor: '#94a3b8',
      },
      grid: {
        vertLines: { color: 'rgba(99,102,241,0.07)' },
        horzLines: { color: 'rgba(99,102,241,0.07)' },
      },
      crosshair: { vertLine: { color: '#6366f1' }, horzLine: { color: '#6366f1' } },
      rightPriceScale: { borderColor: '#1e2235' },
      timeScale: { borderColor: '#1e2235', timeVisible: true, secondsVisible: false },
      autoSize: true,
    });

    this.series = this.chart.addSeries(CandlestickSeries, {
      upColor:   '#22c55e',
      downColor: '#ef4444',
      borderUpColor:   '#22c55e',
      borderDownColor: '#ef4444',
      wickUpColor:   '#22c55e',
      wickDownColor: '#ef4444',
    });

    this.fetchAndRender();
  }

  private fetchAndRender(): void {
    const { yahooTicker, interval: autoInterval, rangeParam: autoRange } = this.resolveDataParams();
    const interval  = this.overrideInterval ?? autoInterval;
    const rangeParam = this.overrideRange    ?? autoRange;
    if (!this.activeTimeframe) {
      // mark the auto-selected timeframe as active
      const match = this.timeframes.find(tf => tf.interval === interval);
      this.activeTimeframe = match?.label ?? '';
    }
    // /yf is proxied to https://query1.finance.yahoo.com in dev (proxy.conf.json)
    // and to the backend /api/yf passthrough in production.
    const url = `/yf/v8/finance/chart/${encodeURIComponent(yahooTicker)}?interval=${interval}&range=${rangeParam}&includePrePost=false`;

    this.http.get<any>(url).subscribe({
      next: (raw) => this.zone.runOutsideAngular(() => this.applyData(raw)),
      error: () => {
        this.zone.run(() => { this.error = 'Could not load chart data'; });
      }
    });
  }

  private applyData(raw: any): void {
    const result = raw?.chart?.result?.[0];
    if (!result) return;

    const timestamps: number[] = result.timestamp ?? [];
    const q = result.indicators?.quote?.[0] ?? {};
    const opens: number[]  = q.open   ?? [];
    const highs: number[]  = q.high   ?? [];
    const lows: number[]   = q.low    ?? [];
    const closes: number[] = q.close  ?? [];

    const candles: CandlestickData[] = [];
    for (let i = 0; i < timestamps.length; i++) {
      if (opens[i] == null || highs[i] == null || lows[i] == null || closes[i] == null) continue;
      candles.push({
        time: timestamps[i] as UTCTimestamp,
        open:  opens[i],
        high:  highs[i],
        low:   lows[i],
        close: closes[i],
      });
    }

    if (!candles.length || !this.series || !this.chart) return;
    this.clearAnnotations();

    // Convert to sequential indices to eliminate overnight/weekend session gaps
    this.timestampMap = candles.map(c => c.time as number);
    const tsMap = this.timestampMap;
    const intervalSec = tsMap.length > 1 ? tsMap[1] - tsMap[0] : 60;
    const seqCandles: CandlestickData[] = candles.map((c, i) => ({ ...c, time: i as UTCTimestamp }));

    this.chart.applyOptions({
      timeScale: {
        tickMarkFormatter: (idx: any) => {
          const i = idx as number;
          const ts = tsMap[i];
          if (ts == null) return '';
          const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
          const d = new Date(ts * 1000);
          const prevTs = i > 0 ? tsMap[i - 1] : null;
          const isNewDay = !prevTs || Math.floor(ts / 86400) !== Math.floor(prevTs / 86400);
          if (intervalSec >= 86400 || isNewDay) {
            return `${d.getUTCDate()} ${months[d.getUTCMonth()]}`;
          }
          return `${String(d.getUTCHours()).padStart(2,'0')}:${String(d.getUTCMinutes()).padStart(2,'0')}`;
        }
      }
    });

    this.series.setData(seqCandles);

    // ── Entry price line ─────────────────────────────────────────────────────
    this.entryLine = this.series.createPriceLine({
      price: this.entryPrice,
      color: '#6366f1',
      lineWidth: 1,
      lineStyle: LineStyle.Dashed,
      axisLabelVisible: true,
      title: 'Entry',
    });

    // ── Exit price line ───────────────────────────────────────────────────────
    if (this.exitPrice != null) {
      const exitColor = this.exitPrice > this.entryPrice ? '#22c55e' : '#ef4444';
      this.exitLine = this.series.createPriceLine({
        price: this.exitPrice,
        color: exitColor,
        lineWidth: 1,
        lineStyle: LineStyle.Dashed,
        axisLabelVisible: true,
        title: 'Exit',
      });
    }

    // ── Markers on the nearest candle to entry / exit ────────────────────────
    const entryTs  = Math.floor(new Date(this.entryTime).getTime() / 1000);
    const exitTs   = this.exitTime ? Math.floor(new Date(this.exitTime).getTime() / 1000) : null;
    const nearestIdx = (target: number): UTCTimestamp => {
      let best = 0;
      let bestDist = Math.abs(tsMap[0] - target);
      for (let i = 1; i < tsMap.length; i++) {
        const dist = Math.abs(tsMap[i] - target);
        if (dist < bestDist) { bestDist = dist; best = i; }
      }
      return best as UTCTimestamp;
    };

    const entryMarkerTime = nearestIdx(entryTs);
    const isLong = this.direction === 'Long';

    const markers: any[] = [{
      time: entryMarkerTime,
      position: isLong ? 'belowBar' : 'aboveBar',
      color: '#6366f1',
      shape: isLong ? 'arrowUp' : 'arrowDown',
      text: `Entry ${this.entryPrice}`,
      size: 1,
    }];

    if (exitTs != null && this.exitPrice != null) {
      const exitMarkerTime = nearestIdx(exitTs);
      const exitColor = this.exitPrice > this.entryPrice ? '#22c55e' : '#ef4444';
      markers.push({
        time: exitMarkerTime,
        position: isLong ? 'aboveBar' : 'belowBar',
        color: exitColor,
        shape: isLong ? 'arrowDown' : 'arrowUp',
        text: `Exit ${this.exitPrice}`,
        size: 1,
      });
    }

    this.markersPlugin = createSeriesMarkers(this.series, markers);

    // ── Show all loaded candles ───────────────────────────────────────────────
    this.chart!.timeScale().fitContent();
  }

  private clearAnnotations(): void {
    if (this.entryLine && this.series) {
      this.series.removePriceLine(this.entryLine);
      this.entryLine = undefined;
    }
    if (this.exitLine && this.series) {
      this.series.removePriceLine(this.exitLine);
      this.exitLine = undefined;
    }
    if (this.markersPlugin) {
      this.markersPlugin.setMarkers([]);
      this.markersPlugin = undefined;
    }
  }

  private tradeDurationSec(): number {
    const entry = Math.floor(new Date(this.entryTime).getTime() / 1000);
    const exit  = this.exitTime
      ? Math.floor(new Date(this.exitTime).getTime() / 1000)
      : entry + 3600;
    return Math.max(exit - entry, 3600);
  }

  /** Resolve Yahoo Finance ticker, interval, and range for the trade. */
  private resolveDataParams(): { yahooTicker: string; interval: string; rangeParam: string } {
    const s = this.symbol.toUpperCase();
    const durSec = this.tradeDurationSec();

    let yahooTicker = this.toYahooTicker(s);

    let interval: string;
    let rangeParam: string;

    if (durSec < 3600) {               // < 1 h  → 1m candles, 1d range
      interval = '1m'; rangeParam = '1d';
    } else if (durSec < 4 * 3600) {    // < 4 h  → 5m, 5d
      interval = '5m'; rangeParam = '5d';
    } else if (durSec < 86400) {       // < 1 d  → 15m, 5d
      interval = '15m'; rangeParam = '5d';
    } else if (durSec < 7 * 86400) {   // < 1 wk → 1h, 1mo
      interval = '1h'; rangeParam = '1mo';
    } else {                            // longer → 1d, 3mo
      interval = '1d'; rangeParam = '3mo';
    }

    return { yahooTicker, interval, rangeParam };
  }

  private toYahooTicker(s: string): string {
    // Metals
    if (s === 'XAUUSD' || s === 'GOLD')   return 'GC=F';
    if (s === 'XAGUSD' || s === 'SILVER') return 'SI=F';
    if (s === 'XPTUSD')                   return 'PL=F';
    if (s === 'XTIUSD' || s === 'OIL' || s === 'USOIL') return 'CL=F';
    if (s === 'XNGUSD' || s === 'NGAS')  return 'NG=F';

    // Indices
    const indices: Record<string, string> = {
      US30: 'YM=F',    DJ30: 'YM=F',    DOW30: 'YM=F',
      US500: 'ES=F',   SP500: 'ES=F',   SPX500: 'ES=F', SPX: '^GSPC',
      US100: 'NQ=F',   NAS100: 'NQ=F',  NASDAQ100: 'NQ=F',
      US2000: 'RTY=F',
      GER40: '^GDAXI', DAX40: '^GDAXI', GER30: '^GDAXI',
      UK100: '^FTSE',  FTSE100: '^FTSE',
      AUS200: '^AXJO',
      JPN225: '^N225',
      HK50: '^HSI',
      EU50: '^STOXX50E',
    };
    if (indices[s]) return indices[s];

    // Crypto → Binance (handled differently: use Binance public API)
    if (/USDT$|USDC$/.test(s)) return `${s}-USD`;  // Yahoo has BTC-USD etc
    if (/^BTC/.test(s) && !s.includes('USD')) return 'BTC-USD';
    if (/^ETH/.test(s) && !s.includes('USD')) return 'ETH-USD';

    // Forex — Yahoo uses EURUSD=X format
    const fxCurrencies = ['EUR','GBP','USD','JPY','CHF','AUD','NZD','CAD',
                          'SEK','NOK','DKK','SGD','HKD','MXN','TRY','ZAR','CNH'];
    const clean = s.endsWith('M') ? s.slice(0, -1) : s; // strip broker suffix
    if (clean.length === 6 &&
        fxCurrencies.some(c => clean.startsWith(c)) &&
        fxCurrencies.some(c => clean.endsWith(c))) {
      return `${clean}=X`;
    }

    // Stock — pass through as-is
    return s;
  }
}
