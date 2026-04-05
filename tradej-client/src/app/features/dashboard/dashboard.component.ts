import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChartModule } from 'primeng/chart';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ProgressBarModule } from 'primeng/progressbar';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DashboardService } from '../../core/services/dashboard.service';
import { AccountService } from '../../core/services/account.service';
import { Dashboard, DashboardSummary, MonthlyStats, DailyStats } from '../../core/models/dashboard.model';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, ChartModule, SelectModule, TableModule, TagModule, ProgressBarModule, ButtonModule, ToastModule],
  providers: [MessageService],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private accountService = inject(AccountService);
  private http = inject(HttpClient);
  private messageService = inject(MessageService);

  syncing = signal(false);

  dashboard = signal<Dashboard | null>(null);
  loading = signal(false);
  accountId = signal<number | null>(null);
  accountCurrency = signal<string>('USD');
  accountInitialBalance = signal<number>(0);

  // ---- Cumulative P&L chart ----
  cumulativePnLChartData = computed(() => {
    const d = this.dashboard();
    if (!d || d.dailyStats.length === 0) return null;
    const sorted = [...d.dailyStats].sort((a, b) => a.date.localeCompare(b.date));
    let cum = 0;
    const values = sorted.map(s => { cum += s.netPnL; return +cum.toFixed(2); });
    const labels = sorted.map(s => {
      const dt = new Date(s.date + 'T00:00:00');
      return dt.toLocaleDateString('en-US', { month: '2-digit', day: '2-digit', year: '2-digit' });
    });
    const lastVal = values[values.length - 1] ?? 0;
    const color   = lastVal >= 0 ? '#6366f1' : '#F06363';
    const bgColor = lastVal >= 0 ? 'rgba(99,102,241,0.15)' : 'rgba(239,68,68,0.15)';
    return {
      labels,
      datasets: [{ label: 'Cumulative P&L', data: values, borderColor: color,
        backgroundColor: bgColor, borderWidth: 2, fill: true, tension: 0.3,
        pointRadius: 3, pointHoverRadius: 5 }]
    };
  });

  // ---- Account balance chart ----
  accountBalanceChartData = computed(() => {
    const d    = this.dashboard();
    const init = this.accountInitialBalance();
    if (!d || d.dailyStats.length === 0) return null;
    const sorted = [...d.dailyStats].sort((a, b) => a.date.localeCompare(b.date));
    let cum = 0;
    const balances = sorted.map(s => { cum += s.netPnL; return +(init + cum).toFixed(2); });
    const labels = sorted.map(s => {
      const dt = new Date(s.date + 'T00:00:00');
      return dt.toLocaleDateString('en-US', { month: '2-digit', day: '2-digit', year: '2-digit' });
    });
    return {
      labels,
      datasets: [
        { label: 'Account Balance', data: balances, borderColor: '#6366f1',
          backgroundColor: 'rgba(99,102,241,0.08)', borderWidth: 2, fill: false,
          tension: 0.3, pointRadius: 3, pointHoverRadius: 5 },
        { label: 'Initial Balance', data: Array(labels.length).fill(init),
          borderColor: '#F06363', backgroundColor: 'transparent',
          borderWidth: 2, borderDash: [4, 4], fill: false,
          pointRadius: 0, tension: 0 }
      ]
    };
  });

  lineChartOptions = {
    responsive: true, maintainAspectRatio: false, animation: false,
    plugins: {
      legend: { display: false },
      tooltip: { callbacks: { label: (ctx: any) => ` $${ctx.raw.toFixed(2)}` } }
    },
    scales: {
      x: { ticks: { color: '#64748b', maxTicksLimit: 8 }, grid: { color: '#1e2235' } },
      y: { ticks: { color: '#64748b', callback: (v: any) => '$' + v.toLocaleString() }, grid: { color: '#1e2235' } }
    }
  };

  balanceLineOptions = {
    responsive: true, maintainAspectRatio: false, animation: false,
    plugins: {
      legend: {
        display: true,
        labels: { color: '#94a3b8', boxWidth: 12, font: { size: 12 } }
      },
      tooltip: { callbacks: { label: (ctx: any) => ` $${ctx.raw.toLocaleString()}` } }
    },
    scales: {
      x: { ticks: { color: '#64748b', maxTicksLimit: 8 }, grid: { color: '#1e2235' } },
      y: { ticks: { color: '#64748b', callback: (v: any) => '$' + v.toLocaleString() }, grid: { color: '#1e2235' } }
    }
  };

  avgWinLossRatio = computed(() => {
    const s = this.dashboard()?.summary;
    if (!s || s.averageLoss === 0) return '—';
    return (s.averageWin / Math.abs(s.averageLoss)).toFixed(2);
  });

  avgWinBarPct = computed(() => {
    const s = this.dashboard()?.summary;
    if (!s) return 50;
    const total = s.averageWin + Math.abs(s.averageLoss);
    return total === 0 ? 50 : Math.round(s.averageWin / total * 100);
  });

  largestWinBarPct = computed(() => {
    const s = this.dashboard()?.summary;
    if (!s) return 50;
    const total = s.largestWin + Math.abs(s.largestLoss);
    return total === 0 ? 50 : Math.round(s.largestWin / total * 100);
  });

  selectedYear = new Date().getFullYear();
  selectedMonth: number | null = null;

  yearOptions = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);
  monthOptions = [
    { label: 'All months', value: null },
    ...Array.from({ length: 12 }, (_, i) => ({
      label: new Date(2000, i).toLocaleString('default', { month: 'long' }),
      value: i + 1
    }))
  ];

  barChartOptions = {
    responsive: true,
    plugins: { legend: { display: false } },
    scales: {
      x: { ticks: { color: '#64748b' }, grid: { color: '#1e2235' } },
      y: { ticks: { color: '#64748b' }, grid: { color: '#1e2235' } }
    }
  };

  horizontalBarOptions = {
    indexAxis: 'y',
    responsive: true,
    plugins: { legend: { display: false } },
    scales: {
      x: { ticks: { color: '#64748b' }, grid: { color: '#1e2235' } },
      y: { ticks: { color: '#64748b' }, grid: { color: '#1e2235' } }
    }
  };

  monthlyChartData = computed(() => {
    const d = this.dashboard();
    if (!d) return {};
    const colors = d.monthlyStats.map(m => m.netPnL >= 0 ? '#2FA87A' : '#F06363');
    return {
      labels: d.monthlyStats.map(m => m.monthName.slice(0, 3)),
      datasets: [{
        data: d.monthlyStats.map(m => m.netPnL),
        backgroundColor: colors,
        borderRadius: 4
      }]
    };
  });

  symbolChartData = computed(() => {
    const d = this.dashboard();
    if (!d) return {};
    const top = d.symbolStats.slice(0, 10);
    const colors = top.map(s => s.netPnL >= 0 ? '#2FA87A' : '#F06363');
    return {
      labels: top.map(s => s.symbol),
      datasets: [{
        data: top.map(s => s.netPnL),
        backgroundColor: colors,
        borderRadius: 4
      }]
    };
  });

  // ---- Big calendar (Tradezella-style) ----
  calYear  = signal(new Date().getFullYear());
  calMonth = signal(new Date().getMonth() + 1); // 1-based

  calMonthLabel = computed(() =>
    new Date(this.calYear(), this.calMonth() - 1, 1)
      .toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
  );

  calMonthStats = computed(() => {
    const d = this.dashboard();
    if (!d) return null;
    const prefix = `${this.calYear()}-${String(this.calMonth()).padStart(2, '0')}-`;
    const days = d.dailyStats.filter(s => s.date.startsWith(prefix));
    return {
      netPnL:      days.reduce((s, dd) => s + dd.netPnL, 0),
      tradingDays: days.length
    };
  });

  calendarRows = computed(() => {
    const d = this.dashboard();
    if (!d) return [];
    const year    = this.calYear();
    const month   = this.calMonth();
    const today   = new Date().toISOString().split('T')[0];
    const dailyMap = new Map(d.dailyStats.map(s => [s.date, s]));
    const firstDay    = new Date(year, month - 1, 1);
    const daysInMonth = new Date(year, month, 0).getDate();
    const startPad    = firstDay.getDay(); // 0 = Sun

    const cells: any[] = [];
    for (let i = 0; i < startPad; i++) cells.push(null);
    for (let day = 1; day <= daysInMonth; day++) {
      const dateStr = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
      const stats = dailyMap.get(dateStr) ?? null;
      cells.push({ day, date: dateStr, isToday: dateStr === today, stats });
    }
    while (cells.length % 7 !== 0) cells.push(null);

    const rows: any[] = [];
    for (let i = 0; i < cells.length; i += 7) {
      const week = cells.slice(i, i + 7);
      const tradingDays = week.filter((c: any) => c?.stats);
      const weekPnL = tradingDays.reduce((s: number, c: any) => s + c.stats.netPnL, 0);
      rows.push({ cells: week, weekNum: rows.length + 1, weekPnL, tradingDays: tradingDays.length });
    }
    return rows;
  });

  prevCalMonth(): void {
    if (this.calMonth() === 1) { this.calYear.update(y => y - 1); this.calMonth.set(12); }
    else this.calMonth.update(m => m - 1);
    this.syncCalToFilter();
  }

  nextCalMonth(): void {
    if (this.calMonth() === 12) { this.calYear.update(y => y + 1); this.calMonth.set(1); }
    else this.calMonth.update(m => m + 1);
    this.syncCalToFilter();
  }

  goToThisMonth(): void {
    this.calYear.set(new Date().getFullYear());
    this.calMonth.set(new Date().getMonth() + 1);
    this.syncCalToFilter();
  }

  private syncCalToFilter(): void {
    if (this.calYear() !== this.selectedYear) {
      this.selectedYear = this.calYear();
      this.selectedMonth = null;
      this.load();
    }
  }

  ngOnInit(): void {
    this.accountService.selectedAccount$.subscribe(account => {
      this.accountId.set(account?.id ?? null);
      this.accountCurrency.set(account?.currency ?? 'USD');
      this.accountInitialBalance.set(account?.initialBalance ?? 0);
      if (account) this.load();
    });
  }

  private getCurrencySymbol(currency: string): string {
    const map: Record<string, string> = {
      USD: '$', EUR: '€', GBP: '£', JPY: '¥',
      BTC: '₿', USDT: '$', ETH: 'Ξ'
    };
    return map[currency] ?? currency + ' ';
  }

  load(): void {
    const id = this.accountId();
    if (!id) return;
    this.loading.set(true);
    this.dashboardService.getDashboard(id, this.selectedYear, this.selectedMonth ?? undefined)
      .subscribe({
        next: data => { this.dashboard.set(data); this.loading.set(false); },
        error: () => this.loading.set(false)
      });
  }

  formatPnL(value: number): string {
    const sym = this.getCurrencySymbol(this.accountCurrency());
    const abs = Math.abs(value);
    return (value < 0 ? '-' : '+') + sym + abs.toFixed(2);
  }

  formatPnLShort(value: number): string {
    const sym = this.getCurrencySymbol(this.accountCurrency());
    const abs = Math.abs(value);
    return (value < 0 ? '-' : '+') + sym + (abs >= 1000 ? (abs / 1000).toFixed(1) + 'k' : abs.toFixed(0));
  }

  pnlClass(value: number): string {
    if (value > 0) return 'pnl-pos';
    if (value < 0) return 'pnl-neg';
    return 'pnl-zero';
  }

  resyncAll(): void {
    if (this.syncing()) return;
    this.syncing.set(true);
    this.http.post<void>('/api/sync/all', null).subscribe({
      next: () => {
        this.syncing.set(false);
        this.messageService.add({ severity: 'success', summary: 'Sync complete', detail: 'All accounts have been resynced.', life: 3000 });
        if (this.accountId()) this.load();
      },
      error: () => {
        this.syncing.set(false);
        this.messageService.add({ severity: 'error', summary: 'Sync failed', detail: 'Could not reach the sync service. Check that the MT5 bridge is running.', life: 5000 });
      }
    });
  }
}
