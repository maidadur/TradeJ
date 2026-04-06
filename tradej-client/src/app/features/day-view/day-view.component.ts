import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ChartModule } from 'primeng/chart';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { EditorModule } from 'primeng/editor';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { Subject, debounceTime, mergeMap, map } from 'rxjs';
import { DayViewService } from '../../core/services/day-view.service';
import { AccountService } from '../../core/services/account.service';
import { DayGroup, DayStats, DayTagDef, DayTradeItem, DayViewData, WeekGroup } from '../../core/models/day-view.model';

type ViewMode = 'day' | 'week';

@Component({
  selector: 'app-day-view',
  standalone: true,
  imports: [CommonModule, FormsModule, ChartModule, ButtonModule, SelectModule, TagModule, EditorModule, DialogModule, TooltipModule],
  templateUrl: './day-view.component.html',
  styleUrl: './day-view.component.scss'
})
export class DayViewComponent implements OnInit {
  private dayViewService = inject(DayViewService);
  private accountService = inject(AccountService);
  private router = inject(Router);

  data = signal<DayViewData | null>(null);
  loading = signal(false);
  accountIds = signal<number[]>([]);

  viewMode: ViewMode = 'day';
  selectedYear = new Date().getFullYear();
  selectedMonth: number | null = null;

  expandedDays = new Set<string>();
  expandedWeeks = new Set<number>();

  // Notes state: map of date -> html content
  dayNotes = new Map<string, string>();
  savingNotes = signal<Set<string>>(new Set());
  private noteSave$ = new Subject<{ date: string; content: string }>();

  // Day tags state: map of date -> tagId[]
  dayTags = new Map<string, number[]>();
  dayTagDefs = signal<DayTagDef[]>([]);
  openTagPickerDate: string | null = null;
  tagFilterText: Record<string, string> = {};
  newTagName = '';
  newTagColor = '#6366f1';

  tagMap = computed(() => {
    const m = new Map<number, DayTagDef>();
    for (const t of this.dayTagDefs()) m.set(t.id, t);
    return m;
  });

  // ---- Tag statistics ----
  tagStats = computed(() => {
    const ds = this.days();
    const total = ds.length;
    if (total === 0) return [];
    const counts = new Map<number, number>();
    for (const d of ds) {
      for (const tid of (d.tagIds ?? [])) {
        counts.set(tid, (counts.get(tid) ?? 0) + 1);
      }
    }
    const tagM = this.tagMap();
    return [...counts.entries()]
      .map(([id, count]) => ({
        id,
        name: tagM.get(id)?.name ?? `Tag ${id}`,
        color: tagM.get(id)?.color ?? '#6366f1',
        count,
        pct: Math.round((count / total) * 100)
      }))
      .sort((a, b) => b.count - a.count);
  });

  donutChartData = computed(() => {
    const stats = this.tagStats();
    if (stats.length === 0) return null;
    return {
      labels: stats.map(s => s.name),
      datasets: [{
        data: stats.map(s => s.count),
        backgroundColor: stats.map(s => s.color),
        borderColor: '#1e293b',
        borderWidth: 2
      }]
    };
  });

  donutChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (ctx: any) => {
            const stats = this.tagStats();
            const s = stats[ctx.dataIndex];
            return ` ${s?.name}: ${s?.count} day${s?.count !== 1 ? 's' : ''} (${s?.pct}%)`;
          }
        }
      }
    }
  };

  // Note dialog
  noteDialog = signal<{ visible: boolean; day: DayGroup | null }>(
    { visible: false, day: null }
  );
  noteDialogContent = '';

  yearOptions = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);
  monthOptions = [
    { label: 'All months', value: null },
    ...Array.from({ length: 12 }, (_, i) => ({
      label: new Date(2000, i).toLocaleString('default', { month: 'long' }),
      value: i + 1
    }))
  ];

  // Calendar small display
  calendarMonth = signal(new Date().getMonth() + 1);
  calendarYear  = signal(new Date().getFullYear());

  ngOnInit(): void {
    // Auto-save notes with debounce per date
    this.noteSave$.pipe(
      debounceTime(1500),
      mergeMap(({ date, content }) => {
        return this.dayViewService.saveNote(date, content).pipe(
          map(() => date)
        );
      })
    ).subscribe(date => {
      this.savingNotes.update(s => { const n = new Set(s); n.delete(date); return n; });
    });

    this.accountService.selectedAccountIds$.subscribe(ids => {
      this.accountIds.set(ids);
      if (ids.length) {
        this.load();
        this.dayViewService.getDayTagDefs().subscribe(defs => this.dayTagDefs.set(defs));
      }
    });
  }

  load(): void {
    const id = this.accountIds();
    if (!id.length) return;
    this.loading.set(true);
    this.dayViewService.getDayView(id, this.selectedYear, this.selectedMonth ?? undefined)
      .subscribe({
        next: result => {
          this.data.set(result);
          this.loading.set(false);
          // Populate notes map from server data
          this.dayNotes.clear();
          for (const day of result.days) {
            if (day.note) this.dayNotes.set(day.date, day.note);
            this.dayTags.set(day.date, day.tagIds ?? []);
          }
          // Auto-expand first day
          if (result.days.length > 0) {
            this.expandedDays.add(result.days[0].date);
          }
        },
        error: () => this.loading.set(false)
      });
  }

  // ---- Day tags ----
  getTagsForDate(date: string): DayTagDef[] {
    const m = this.tagMap();
    return (this.dayTags.get(date) ?? []).map(id => m.get(id)).filter((t): t is DayTagDef => !!t);
  }

  isTagSelected(date: string, tagId: number): boolean {
    return (this.dayTags.get(date) ?? []).includes(tagId);
  }

  toggleTagPicker(date: string, event: Event): void {
    event.stopPropagation();
    this.openTagPickerDate = this.openTagPickerDate === date ? null : date;
    if (this.openTagPickerDate) this.tagFilterText = {};
  }

  closeTagPicker(): void {
    this.openTagPickerDate = null;
  }

  toggleDayTag(date: string, tag: DayTagDef, event: Event): void {
    event.stopPropagation();
    const current = this.dayTags.get(date) ?? [];
    if (current.includes(tag.id)) {
      this.dayTags.set(date, current.filter(id => id !== tag.id));
      this.dayViewService.removeDayTag(date, tag.id).subscribe();
    } else {
      this.dayTags.set(date, [...current, tag.id]);
      this.dayViewService.addDayTag(date, tag.id).subscribe();
    }
  }

  removeDayTag(date: string, tag: DayTagDef, event: Event): void {
    event.stopPropagation();
    const current = this.dayTags.get(date) ?? [];
    this.dayTags.set(date, current.filter(id => id !== tag.id));
    this.dayViewService.removeDayTag(date, tag.id).subscribe();
  }

  getFilteredTagsForPicker(date: string): DayTagDef[] {
    const text = (this.tagFilterText[date] ?? '').toLowerCase().trim();
    return this.dayTagDefs().filter(t => !text || t.name.toLowerCase().includes(text));
  }

  createAndSelectTag(date: string): void {
    const name = this.newTagName.trim();
    if (!name) return;
    this.dayViewService.createDayTagDef(name, this.newTagColor).subscribe(def => {
      this.dayTagDefs.update(defs => [...defs, def]);
      const current = this.dayTags.get(date) ?? [];
      this.dayTags.set(date, [...current, def.id]);
      this.dayViewService.addDayTag(date, def.id).subscribe();
      this.newTagName = '';
      this.newTagColor = '#6366f1';
    });
  }

  // ---- Notes ----
  hasNote(date: string): boolean {
    const content = this.dayNotes.get(date) ?? '';
    const stripped = content.replace(/<[^>]*>/g, '').trim();
    return stripped.length > 0;
  }

  getNoteContent(date: string): string {
    return this.dayNotes.get(date) ?? '';
  }

  openNoteDialog(day: DayGroup, event: Event): void {
    event.stopPropagation();
    this.noteDialogContent = this.dayNotes.get(day.date) ?? '';
    this.noteDialog.set({ visible: true, day });
  }

  closeNoteDialog(): void {
    this.noteDialog.update(d => ({ ...d, visible: false }));
  }

  onNoteChange(date: string, content: string): void {
    this.dayNotes.set(date, content);
    this.savingNotes.update(s => new Set([...s, date]));
    this.noteSave$.next({ date, content });
  }

  isNoteSaving(date: string): boolean {
    return this.savingNotes().has(date);
  }

  // ---- Day view ----
  days = computed(() => this.data()?.days ?? []);

  toggleDay(date: string): void {
    if (this.expandedDays.has(date)) this.expandedDays.delete(date);
    else this.expandedDays.add(date);
  }

  isDayExpanded(date: string): boolean {
    return this.expandedDays.has(date);
  }

  // ---- Week view ----
  weeks = computed((): WeekGroup[] => {
    const days = this.data()?.days ?? [];
    const map = new Map<number, DayGroup[]>();
    for (const day of days) {
      const arr = map.get(day.weekNumber) ?? [];
      arr.push(day);
      map.set(day.weekNumber, arr);
    }
    return Array.from(map.entries())
      .sort((a, b) => b[0] - a[0])
      .map(([wn, wDays]) => ({
        weekNumber: wn,
        weekLabel: this.weekLabel(wDays),
        days: wDays,
        stats: this.aggregateStats(wDays)
      }));
  });

  toggleWeek(wn: number): void {
    if (this.expandedWeeks.has(wn)) this.expandedWeeks.delete(wn);
    else this.expandedWeeks.add(wn);
  }

  isWeekExpanded(wn: number): boolean {
    return this.expandedWeeks.has(wn);
  }

  private weekLabel(days: DayGroup[]): string {
    if (!days.length) return '';
    const sorted = [...days].sort((a, b) => a.date.localeCompare(b.date));
    const first = new Date(sorted[0].date + 'T00:00:00');
    const last  = new Date(sorted[sorted.length - 1].date + 'T00:00:00');
    const fmt = (d: Date) => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    return `Week ${days[0].weekNumber}: ${fmt(first)} – ${fmt(last)}`;
  }

  private aggregateStats(days: DayGroup[]): DayStats {
    const all = days.flatMap(d => d.trades);
    const wins = all.filter(t => t.netPnL > 0).length;
    const losses = all.filter(t => t.netPnL < 0).length;
    const grossPnL = all.reduce((s, t) => s + t.grossPnL, 0);
    const netPnL   = all.reduce((s, t) => s + t.netPnL, 0);
    const commission = all.reduce((s, t) => s + t.commission, 0);
    const swap     = all.reduce((s, t) => s + t.swap, 0);
    const volume   = all.reduce((s, t) => s + t.volume, 0);
    const totalWin = all.filter(t => t.netPnL > 0).reduce((s, t) => s + t.netPnL, 0);
    const totalLoss = Math.abs(all.filter(t => t.netPnL < 0).reduce((s, t) => s + t.netPnL, 0));
    const pf = totalLoss === 0 ? (totalWin > 0 ? 99.99 : 0) : +(totalWin / totalLoss).toFixed(2);
    return {
      totalTrades: all.length,
      winners: wins,
      losers: losses,
      grossPnL: +grossPnL.toFixed(2),
      netPnL: +netPnL.toFixed(2),
      commission: +commission.toFixed(2),
      swap: +swap.toFixed(2),
      volume: +volume.toFixed(2),
      winRate: all.length > 0 ? +(wins / all.length * 100).toFixed(2) : 0,
      profitFactor: pf
    };
  }

  // ---- Chart ----
  buildChartData(day: DayGroup) {
    const trades = [...day.trades].reverse();
    let cum = 0;
    const values = trades.map((t: DayTradeItem) => {
      cum += t.netPnL;
      return +cum.toFixed(2);
    });
    const labels = trades.map((_: DayTradeItem, i: number) => `#${i + 1}`);
    const lastVal = values[values.length - 1] ?? 0;
    const color = lastVal >= 0 ? '#2FA87A' : '#F06363';
    const bgColor = lastVal >= 0 ? 'rgba(34,197,94,0.12)' : 'rgba(239,68,68,0.12)';
    return {
      labels,
      datasets: [{
        data: values,
        borderColor: color,
        backgroundColor: bgColor,
        borderWidth: 2,
        pointRadius: 3,
        pointHoverRadius: 5,
        fill: true,
        tension: 0.4
      }]
    };
  }

  chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    plugins: { legend: { display: false }, tooltip: {
      callbacks: {
        label: (ctx: any) => ` $${ctx.raw.toFixed(2)}`
      }
    }},
    scales: {
      x: { display: false },
      y: {
        ticks: { color: '#64748b', font: { size: 10 } },
        grid: { color: '#1e2235' }
      }
    }
  };

  // ---- Calendar ----
  calendarDays = computed(() => {
    const days = this.data()?.days ?? [];
    const dailyMap = new Map(days.map((d: DayGroup) => [d.date, d]));
    const year = this.calendarYear();
    const month = this.calendarMonth();
    const firstDay = new Date(year, month - 1, 1);
    const daysInMonth = new Date(year, month, 0).getDate();
    const startPad = firstDay.getDay();
    const result: any[] = [];
    for (let i = 0; i < startPad; i++) result.push(null);
    for (let day = 1; day <= daysInMonth; day++) {
      const dateStr = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
      result.push({ day, date: dateStr, stats: dailyMap.get(dateStr) ?? null });
    }
    return result;
  });

  calMonthLabel = computed(() =>
    new Date(this.calendarYear(), this.calendarMonth() - 1, 1)
      .toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
  );

  prevCalMonth(): void {
    if (this.calendarMonth() === 1) { this.calendarMonth.set(12); this.calendarYear.update(y => y - 1); }
    else this.calendarMonth.update(m => m - 1);
  }

  nextCalMonth(): void {
    if (this.calendarMonth() === 12) { this.calendarMonth.set(1); this.calendarYear.update(y => y + 1); }
    else this.calendarMonth.update(m => m + 1);
  }

  scrollToDay(date: string): void {
    const el = document.getElementById('day-' + date);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.expandedDays.add(date);
    }
  }

  // ---- Formatting ----
  formatPnL(v: number): string {
    return (v < 0 ? '-$' : '$') + Math.abs(v).toFixed(2);
  }

  formatDuration(mins: number): string {
    if (mins < 60) return `${mins}m`;
    return `${Math.floor(mins / 60)}h ${mins % 60}m`;
  }

  formatTime(dt: string): string {
    return new Date(dt).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
  }

  openTrade(id: number): void {
    this.router.navigate(['/trades', id]);
  }

  onFilterChange(): void {
    const m = this.selectedMonth;
    if (m !== null) {
      this.calendarMonth.set(m);
      this.calendarYear.set(this.selectedYear);
    }
    this.load();
  }
}
