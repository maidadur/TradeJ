import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { FileUploadModule } from 'primeng/fileupload';
import { MessageModule } from 'primeng/message';
import { ProgressBarModule } from 'primeng/progressbar';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ImportService, ImportResult } from '../../core/services/import.service';
import { AccountService } from '../../core/services/account.service';
import { Account } from '../../core/models/account.model';
import { CTraderConnectDialogComponent } from './ctrader-connect-dialog/ctrader-connect-dialog.component';
import { SettingsService, SETTING_MT5_SYNC, SETTING_CTRADER_SYNC } from '../../core/services/settings.service';

interface BrokerOption {
  label: string;
  value: 'mt5' | 'ctrader' | 'bybit';
  description: string;
  hint: string;
}

@Component({
  selector: 'app-import',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, SelectModule,
            FileUploadModule, MessageModule, ProgressBarModule, DatePickerModule,
            InputTextModule, ToggleSwitchModule, CTraderConnectDialogComponent],
  templateUrl: './import.component.html',
  styleUrl: './import.component.scss'
})
export class ImportComponent {
  private importService = inject(ImportService);
  private accountService = inject(AccountService);
  private settingsService = inject(SettingsService);

  // Auto-sync toggle state
  mt5SyncEnabled    = signal(true);
  ctraderSyncEnabled = signal(true);

  // cTrader connect dialog
  showCTraderDialog = signal(false);

  // CSV import state
  accounts = signal<Account[]>([]);
  selectedAccount: Account | null = null;
  selectedBroker: 'mt5' | 'ctrader' | 'bybit' | null = null;
  selectedFile = signal<File | null>(null);
  importing = signal(false);
  result = signal<ImportResult | null>(null);

  // Live import state (MetaApi)
  liveAccount: Account | null = null;
  liveDateFrom: Date = (() => { const d = new Date(); d.setDate(d.getDate() - 30); return d; })();
  liveDateTo: Date = new Date();
  importingLive = signal(false);
  liveResult = signal<ImportResult | null>(null);

  // Sync import state (Python bridge)
  syncAccount: Account | null = null;
  syncDateFrom: Date = (() => { const d = new Date(); d.setDate(d.getDate() - 30); return d; })();
  syncDateTo: Date = new Date();
  importingSync = signal(false);
  syncResult = signal<ImportResult | null>(null);

  /** MT5 accounts that have MetaApi credentials configured */
  mt5LiveAccounts = computed(() =>
    this.accounts().filter(a => a.broker === 'MT5' && a.metaApiAccountId && a.hasMetaApiToken)
  );

  /** MT5 accounts ready for sync (server + investor password both configured) */
  mt5SyncAccounts = computed(() =>
    this.accounts().filter(a => a.broker === 'MT5' && a.mt5Server && a.hasMT5InvestorPassword)
  );

  brokerOptions: BrokerOption[] = [
    { label: 'MT5', value: 'mt5', description: 'MetaTrader 5', hint: 'Account history CSV with: Ticket, Open Time, Type, Size, Symbol, Open Price, Close Time, Close Price, Commission, Swap, Profit.' },
    { label: 'cTrader', value: 'ctrader', description: 'cTrader Platform', hint: 'Position history CSV with: Position ID, Symbol, Direction, Volume (Lots), Open/Close Time & Price, Commission, Swap, Gross/Net Profit.' },
    { label: 'ByBit', value: 'bybit', description: 'ByBit Exchange', hint: 'Closed P&L CSV with: Contract, Side, Average Entry Price, Average Exit Price, Closed P&L, Qty, Fill Time.' }
  ];

  constructor() {
    this.accountService.getAll().subscribe(list => {
      this.accounts.set(list);
      if (!this.selectedAccount) this.selectedAccount = this.accountService.selectedAccount;
      // default live account to first MT5 live-enabled account
      const mt5 = this.mt5LiveAccounts();
      if (mt5.length > 0 && !this.liveAccount) this.liveAccount = mt5[0];
      // default sync account to first MT5 sync-enabled account
      const sync = this.mt5SyncAccounts();
      if (sync.length > 0 && !this.syncAccount) this.syncAccount = sync[0];
    });

    this.settingsService.getAll().subscribe(s => {
      this.mt5SyncEnabled.set(s[SETTING_MT5_SYNC] !== 'false');
      this.ctraderSyncEnabled.set(s[SETTING_CTRADER_SYNC] !== 'false');
    });
  }

  onMt5SyncToggle(enabled: boolean): void {
    this.settingsService.update(SETTING_MT5_SYNC, enabled).subscribe();
  }

  onCTraderSyncToggle(enabled: boolean): void {
    this.settingsService.update(SETTING_CTRADER_SYNC, enabled).subscribe();
  }

  canImport(): boolean {
    return !!this.selectedBroker && !!this.selectedAccount && !!this.selectedFile();
  }

  currentBroker(): BrokerOption | undefined {
    return this.brokerOptions.find(b => b.value === this.selectedBroker);
  }

  canLiveImport(): boolean {
    return !!this.liveAccount && !!this.liveDateFrom && !!this.liveDateTo;
  }

  canSyncImport(): boolean {
    return !!this.syncAccount && !!this.syncDateFrom && !!this.syncDateTo;
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    const file = event.dataTransfer?.files[0];
    if (file) this.selectedFile.set(file);
  }

  doImport(): void {
    if (!this.canImport()) return;
    this.importing.set(true);
    this.result.set(null);
    this.importService.import(this.selectedBroker!, this.selectedAccount!.id, this.selectedFile()!)
      .subscribe({
        next: r => { this.result.set(r); this.importing.set(false); },
        error: () => {
          this.result.set({ imported: 0, skipped: 0, errors: 1, errorMessages: ['Server error. Check API connection.'] });
          this.importing.set(false);
        }
      });
  }

  doLiveImport(): void {
    if (!this.canLiveImport()) return;
    this.importingLive.set(true);
    this.liveResult.set(null);
    this.importService.importLive(this.liveAccount!.id, this.liveDateFrom, this.liveDateTo)
      .subscribe({
        next: r => { this.liveResult.set(r); this.importingLive.set(false); },
        error: err => {
          const msg = err.error?.message ?? 'Server error. Check MetaApi credentials.';
          this.liveResult.set({ imported: 0, skipped: 0, errors: 1, errorMessages: [msg] });
          this.importingLive.set(false);
        }
      });
  }

  doSyncImport(): void {
    if (!this.canSyncImport()) return;
    this.importingSync.set(true);
    this.syncResult.set(null);
    this.importService.importSync(this.syncAccount!.id, this.syncDateFrom, this.syncDateTo)
      .subscribe({
        next: r => { this.syncResult.set(r); this.importingSync.set(false); },
        error: err => {
          const msg = err.error?.message ?? 'Sync failed. Make sure the MT5 bridge is running.';
          this.syncResult.set({ imported: 0, skipped: 0, errors: 1, errorMessages: [msg] });
          this.importingSync.set(false);
        }
      });
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }
}
