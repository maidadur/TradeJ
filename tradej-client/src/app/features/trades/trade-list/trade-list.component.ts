import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { DatePickerModule } from 'primeng/datepicker';
import { PaginatorModule } from 'primeng/paginator';
import { Trade, TradeFilter } from '../../../core/models/trade.model';
import { TradeService } from '../../../core/services/trade.service';
import { AccountService } from '../../../core/services/account.service';
import { StrategyService } from '../../../core/services/strategy.service';

@Component({
  selector: 'app-trade-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TableModule, InputTextModule,
    SelectModule, ButtonModule, TagModule, DatePickerModule, PaginatorModule
  ],
  templateUrl: './trade-list.component.html',
  styleUrl: './trade-list.component.scss'
})
export class TradeListComponent implements OnInit {
  private tradeService = inject(TradeService);
  private accountService = inject(AccountService);
  private strategyService = inject(StrategyService);
  private router = inject(Router);

  trades = signal<Trade[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  currentPage = signal(1);
  accountIds = signal<number[]>([]);
  strategyMap = signal<Map<number, string>>(new Map());
  pageSize = 50;

  filter: Partial<TradeFilter> = { sortBy: 'entryTime', sortDesc: true };
  dateFrom: Date | null = null;
  dateTo: Date | null = null;

  directionOptions = ['Long', 'Short'];
  statusOptions = ['Open', 'Closed', 'Cancelled'];

  ngOnInit(): void {
    this.accountService.selectedAccountIds$.subscribe(ids => {
      this.accountIds.set(ids);
      if (ids.length) {
        this.currentPage.set(1);
        this.loadTrades();
        this.strategyService.getAll().subscribe(list => {
            const map = new Map<number, string>();
            list.forEach(s => map.set(s.id, s.name));
            this.strategyMap.set(map);
        });
      }
    });
  }

  loadTrades(): void {
    const ids = this.accountIds();
    if (!ids.length) return;
    this.loading.set(true);
    const f: TradeFilter = {
      accountIds: ids,
      page: this.currentPage(),
      pageSize: this.pageSize,
      symbol: this.filter.symbol,
      direction: this.filter.direction,
      status: this.filter.status,
      dateFrom: this.dateFrom?.toISOString().split('T')[0],
      dateTo: this.dateTo?.toISOString().split('T')[0],
      sortBy: this.filter.sortBy ?? 'entryTime',
      sortDesc: this.filter.sortDesc ?? true,
    };
    this.tradeService.getTrades(f).subscribe({
      next: result => {
        this.trades.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadTrades();
  }

  clearFilters(): void {
    this.filter = {};
    this.dateFrom = null;
    this.dateTo = null;
    this.applyFilters();
  }

  onPageChange(event: any): void {
    this.currentPage.set(Math.floor(event.first / event.rows) + 1);
    this.pageSize = event.rows;
    this.loadTrades();
  }

  openTrade(id: number): void {
    this.router.navigate(['/trades', id]);
  }

  formatPnL(value: number): string {
    const abs = Math.abs(value);
    return (value < 0 ? '-$' : '$') + abs.toFixed(2);
  }

  formatDuration(entryTime: string, exitTime?: string): string {
    if (!exitTime) return '—';
    const mins = Math.round((new Date(exitTime).getTime() - new Date(entryTime).getTime()) / 60000);
    if (mins < 60) return `${mins}m`;
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  pnlClass(value: number): string {
    if (value > 0) return 'pnl-pos';
    if (value < 0) return 'pnl-neg';
    return 'pnl-zero';
  }

  getStrategyNames(ids: number[]): string {
    if (!ids?.length) return '—';
    const map = this.strategyMap();
    return ids.map(id => map.get(id) ?? '?').join(', ');
  }
}
