import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { DatePickerModule } from 'primeng/datepicker';
import { PaginatorModule } from 'primeng/paginator';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { MessageService, ConfirmationService } from 'primeng/api';
import { CreateTradeDto, Trade, TradeFilter } from '../../../core/models/trade.model';
import { Account } from '../../../core/models/account.model';
import { TradeService } from '../../../core/services/trade.service';
import { AccountService } from '../../../core/services/account.service';
import { StrategyService } from '../../../core/services/strategy.service';
import { TradeNavigationService } from '../../../core/services/trade-navigation.service';

@Component({
  selector: 'app-trade-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TableModule, InputTextModule, InputNumberModule,
    SelectModule, ButtonModule, TagModule, DatePickerModule, PaginatorModule, ToastModule,
    ConfirmDialogModule, TooltipModule, DialogModule, TextareaModule, AutoCompleteModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './trade-list.component.html',
  styleUrl: './trade-list.component.scss'
})
export class TradeListComponent implements OnInit {
  private tradeService = inject(TradeService);
  private accountService = inject(AccountService);
  private strategyService = inject(StrategyService);
  private router = inject(Router);
  private tradeNavService = inject(TradeNavigationService);
  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  private confirmService = inject(ConfirmationService);

  trades = signal<Trade[]>([]);
  loading = signal(false);
  syncing = signal(false);
  merging = signal(false);
  totalCount = signal(0);
  currentPage = signal(1);
  accountIds = signal<number[]>([]);
  strategyMap = signal<Map<number, string>>(new Map());
  pageSize = 50;

  selectedTrades: Trade[] = [];

  filter: Partial<TradeFilter> = { sortBy: 'entryTime', sortDesc: true };
  dateFrom: Date | null = null;
  dateTo: Date | null = null;

  directionOptions = ['Long', 'Short'];
  statusOptions = ['Open', 'Closed', 'Cancelled'];

  accounts = signal<Account[]>([]);
  addDialogVisible = signal(false);
  saving = signal(false);
  newTrade: Partial<CreateTradeDto> & { entryTimeDate?: Date | null; exitTimeDate?: Date | null } = {};
  allSymbols: string[] = [];
  symbolSuggestions: string[] = [];

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
    this.accountService.allAccounts$.subscribe(accounts => this.accounts.set(accounts));
  }

  private toLocalDate(d: Date | null): string | undefined {
    if (!d) return undefined;
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  loadTrades(): void {
    const ids = this.accountIds();
    if (!ids.length) return;
    this.loading.set(true);
    this.selectedTrades = [];
    const f: TradeFilter = {
      accountIds: ids,
      page: this.currentPage(),
      pageSize: this.pageSize,
      symbol: this.filter.symbol,
      direction: this.filter.direction,
      status: this.filter.status,
      dateFrom: this.toLocalDate(this.dateFrom),
      dateTo: this.toLocalDate(this.dateTo),
      sortBy: this.filter.sortBy ?? 'entryTime',
      sortDesc: this.filter.sortDesc ?? true,
    };
    this.tradeService.getTrades(f).subscribe({
      next: result => {
        this.trades.set(result.items);
        this.totalCount.set(result.totalCount);
        this.tradeNavService.setTradeIds(result.items.map(t => t.id));
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

  resyncAll(): void {
    if (this.syncing()) return;
    const ids = this.accountIds();
    if (!ids.length) return;
    this.syncing.set(true);
    this.http.post<{ imported: number; skipped: number; errors: number; errorMessages: string[] }>('/api/sync/selected', ids).subscribe({
      next: (result) => {
        this.syncing.set(false);
        if (result.errors > 0) {
          this.messageService.add({ severity: 'warn', summary: 'Sync completed with errors', detail: result.errorMessages[0] ?? 'Some accounts failed to sync.', life: 7000 });
        } else {
          this.messageService.add({ severity: 'success', summary: 'Sync complete', detail: `Imported ${result.imported}, skipped ${result.skipped}.`, life: 4000 });
        }
        this.loadTrades();
      },
      error: (err) => {
        this.syncing.set(false);
        const detail = err.error?.message ?? err.error ?? 'Could not reach the sync service.';
        this.messageService.add({ severity: 'error', summary: 'Sync failed', detail, life: 7000 });
      }
    });
  }

  get mergeBlockReason(): string | null {
    const sel = this.selectedTrades;
    if (sel.length < 2) return null;
    const first = sel[0];
    if (sel.some(t => t.accountId !== first.accountId || t.symbol !== first.symbol || t.direction !== first.direction)) {
      return 'Selected trades must share the same account, symbol, and direction.';
    }
    if (sel.some(t => t.mergedTradeIds?.length)) {
      return "A trade that already contains merged trades can't be merged again.";
    }
    return null;
  }

  get canMerge(): boolean {
    return this.selectedTrades.length >= 2 && !this.mergeBlockReason;
  }

  mergeSelected(): void {
    if (!this.canMerge || this.merging()) return;
    const ids = this.selectedTrades.map(t => t.id);
    const count = ids.length;
    this.confirmService.confirm({
      message: `Merge ${count} trades into one combined trade? The originals stay in the database but will no longer appear in this list. You can undo this later from the merged trade's detail page.`,
      header: 'Merge Trades',
      icon: 'pi pi-clone',
      acceptButtonStyleClass: 'p-button-primary',
      accept: () => {
        this.merging.set(true);
        this.tradeService.mergeTrades(ids).subscribe({
          next: () => {
            this.merging.set(false);
            this.selectedTrades = [];
            this.messageService.add({ severity: 'success', summary: 'Trades merged', detail: `${count} trades combined into one.`, life: 3000 });
            this.loadTrades();
          },
          error: (err) => {
            this.merging.set(false);
            const detail = err.error?.message ?? err.error ?? 'Could not merge trades.';
            this.messageService.add({ severity: 'error', summary: 'Merge failed', detail, life: 5000 });
          }
        });
      }
    });
  }

  openAddDialog(): void {
    const ids = this.accountIds();
    this.newTrade = {
      accountId: ids.length === 1 ? ids[0] : undefined,
      direction: 'Long',
      entryTimeDate: new Date(),
      volume: undefined,
      netPnL: undefined,
      commission: 0,
      swap: 0,
    };
    this.addDialogVisible.set(true);
    this.tradeService.getSymbols(ids).subscribe(symbols => this.allSymbols = symbols);
  }

  filterSymbols(event: { query: string }): void {
    const q = event.query.trim().toUpperCase();
    this.symbolSuggestions = q
      ? this.allSymbols.filter(s => s.toUpperCase().includes(q))
      : this.allSymbols;
  }

  get addDialogVisibleProxy(): boolean {
    return this.addDialogVisible();
  }
  set addDialogVisibleProxy(value: boolean) {
    this.addDialogVisible.set(value);
  }

  get canSaveNewTrade(): boolean {
    const t = this.newTrade;
    return !!(t.accountId && t.symbol?.trim() && t.direction &&
      t.volume != null && t.netPnL != null);
  }

  saveNewTrade(): void {
    if (!this.canSaveNewTrade || this.saving()) return;
    const t = this.newTrade;
    const dto: CreateTradeDto = {
      accountId: t.accountId!,
      symbol: t.symbol!.trim().toUpperCase(),
      direction: t.direction as 'Long' | 'Short',
      volume: t.volume!,
      netPnL: t.netPnL!,
      entryTime: t.entryTimeDate ? t.entryTimeDate.toISOString() : null,
      entryPrice: t.entryPrice ?? null,
      exitTime: t.exitTimeDate ? t.exitTimeDate.toISOString() : null,
      exitPrice: t.exitPrice ?? null,
      stopLoss: t.stopLoss ?? null,
      takeProfit: t.takeProfit ?? null,
      commission: t.commission ?? 0,
      swap: t.swap ?? 0,
      notes: t.notes ?? null,
    };
    this.saving.set(true);
    this.tradeService.createTrade(dto).subscribe({
      next: () => {
        this.saving.set(false);
        this.addDialogVisible.set(false);
        this.messageService.add({ severity: 'success', summary: 'Trade added', life: 3000 });
        this.loadTrades();
      },
      error: (err) => {
        this.saving.set(false);
        const detail = err.error?.message ?? err.error ?? 'Could not add trade.';
        this.messageService.add({ severity: 'error', summary: 'Add trade failed', detail, life: 5000 });
      }
    });
  }

  isManual(trade: Trade): boolean {
    return trade.brokerTradeId?.startsWith('MANUAL-') ?? false;
  }

  deleteTrade(trade: Trade): void {
    this.confirmService.confirm({
      message: `Delete this manually added ${trade.symbol} trade? This cannot be undone.`,
      header: 'Delete Trade',
      icon: 'pi pi-trash',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.tradeService.deleteTrade(trade.id).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Trade deleted', life: 3000 });
            this.loadTrades();
          },
          error: (err) => {
            const detail = err.error?.message ?? err.error ?? 'Could not delete trade.';
            this.messageService.add({ severity: 'error', summary: 'Delete failed', detail, life: 5000 });
          }
        });
      }
    });
  }

  exportCsv(): void {
    this.export('csv');
  }

  exportPdf(): void {
    this.export('pdf');
  }

  private export(kind: 'csv' | 'pdf'): void {
    const ids = this.accountIds();
    if (!ids.length) return;
    const f = {
      accountIds: ids,
      symbol: this.filter.symbol,
      direction: this.filter.direction,
      status: this.filter.status,
      dateFrom: this.toLocalDate(this.dateFrom),
      dateTo: this.toLocalDate(this.dateTo),
      sortBy: this.filter.sortBy ?? 'entryTime',
      sortDesc: this.filter.sortDesc ?? true,
    };
    const request = kind === 'pdf' ? this.tradeService.exportPdf(f) : this.tradeService.exportCsv(f);
    request.subscribe({
      next: response => {
        const blob = response.body!;
        const disposition = response.headers.get('Content-Disposition') ?? '';
        const match = disposition.match(/filename="?([^";\n]+)"?/);
        const filename = match ? match[1] : `trades.${kind}`;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.messageService.add({ severity: 'error', summary: 'Export failed', detail: 'Could not export trades.', life: 5000 })
    });
  }
}
