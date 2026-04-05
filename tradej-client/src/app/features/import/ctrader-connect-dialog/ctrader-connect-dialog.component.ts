import {
  Component, inject, signal, computed, OnDestroy, Input, Output, EventEmitter
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ProgressBarModule } from 'primeng/progressbar';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { CTraderService, CTraderAccount, CTraderLinkRequest } from '../../../core/services/ctrader.service';
import { AccountService } from '../../../core/services/account.service';
import { Account } from '../../../core/models/account.model';
import { ImportResult } from '../../../core/services/import.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

interface AccountRow {
  ctraderAccount: CTraderAccount;
  selected: boolean;
  tradeJAccount: Account | null;
  dateFrom: Date;
  dateTo: Date;
  result: ImportResult | null;
  importing: boolean;
  error: string | null;
}

type Step = 'idle' | 'connecting' | 'selecting' | 'done';

@Component({
  selector: 'app-ctrader-connect-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    ButtonModule, DialogModule, SelectModule, DatePickerModule,
    ProgressBarModule, CheckboxModule, MessageModule,
  ],
  templateUrl: './ctrader-connect-dialog.component.html',
  styleUrl: './ctrader-connect-dialog.component.scss',
})
export class CTraderConnectDialogComponent implements OnDestroy {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  private ctrader  = inject(CTraderService);
  private accounts = inject(AccountService);

  step         = signal<Step>('idle');
  errorMsg     = signal<string | null>(null);
  accessToken  = signal<string>('');
  refreshToken = signal<string>('');
  accountRows  = signal<AccountRow[]>([]);
  tradeJAccounts = signal<Account[]>([]);
  importing    = signal(false);

  /** Whether all selected rows have a TradeJ account assigned */
  canImport = computed(() =>
    this.accountRows().some(r => r.selected) &&
    this.accountRows().filter(r => r.selected).every(r => r.tradeJAccount !== null)
  );

  private popup: Window | null = null;
  private msgHandler = this.onPopupMessage.bind(this);

  constructor() {
    this.accounts.getAll().subscribe(list => this.tradeJAccounts.set(list));
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.msgHandler);
    this.popup?.close();
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
    this.reset();
  }

  connectOAuth(): void {
    this.errorMsg.set(null);
    this.step.set('connecting');

    this.ctrader.getOAuthUrl().subscribe({
      next: ({ url }) => {
        window.addEventListener('message', this.msgHandler);
        const w = 600, h = 700;
        const left = window.screenX + (window.outerWidth - w) / 2;
        const top  = window.screenY + (window.outerHeight - h) / 2;
        this.popup = window.open(
          url,
          'ctrader_oauth',
          `width=${w},height=${h},left=${left},top=${top},toolbar=no,menubar=no`
        );
        if (!this.popup) {
          this.errorMsg.set('Popup was blocked. Please allow popups for this site.');
          this.step.set('idle');
          window.removeEventListener('message', this.msgHandler);
        }
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Could not retrieve OAuth URL. Check CTrader config in appsettings.json.');
        this.step.set('idle');
      },
    });
  }

  private onPopupMessage(event: MessageEvent): void {
    if (event.origin !== window.location.origin) return;
    if (event.data?.type !== 'ctrader_oauth') return;

    window.removeEventListener('message', this.msgHandler);

    if (event.data.error) {
      this.errorMsg.set(`cTrader authorization denied: ${event.data.error}`);
      this.step.set('idle');
      return;
    }

    const code = event.data.code as string;
    if (!code) {
      this.errorMsg.set('No authorization code received.');
      this.step.set('idle');
      return;
    }

    this.ctrader.exchangeCode(code).subscribe({
      next: res => {
        this.accessToken.set(res.accessToken);
        this.refreshToken.set(res.refreshToken);
        const defaultFrom = (() => { const d = new Date(); d.setDate(d.getDate() - 30); return d; })();
        const rows: AccountRow[] = res.accounts.map(a => ({
          ctraderAccount: a,
          selected: true,
          tradeJAccount: null,
          dateFrom: new Date(defaultFrom),
          dateTo:   new Date(),
          result:   null,
          importing: false,
          error:    null,
        }));
        this.accountRows.set(rows);
        this.step.set('selecting');
      },
      error: err => {
        this.errorMsg.set(err.error?.message ?? 'Failed to list accounts. Check your cTrader app settings.');
        this.step.set('idle');
      },
    });
  }

  doImport(): void {
    const selected = this.accountRows().filter(r => r.selected && r.tradeJAccount !== null);
    if (!selected.length) return;

    this.importing.set(true);

    // Import sequentially to avoid hammering the WS connection limit
    const importNext = (idx: number): void => {
      if (idx >= selected.length) {
        this.importing.set(false);
        this.step.set('done');
        return;
      }

      const row = selected[idx];
      this.updateRow(row.ctraderAccount.ctidTraderAccountId, r => ({ ...r, importing: true, error: null }));

      this.ctrader.importAccount({
        accessToken:          this.accessToken(),
        ctidTraderAccountId:  row.ctraderAccount.ctidTraderAccountId,
        isLive:               row.ctraderAccount.isLive,
        tradeJAccountId:      row.tradeJAccount!.id,
        dateFrom:             row.dateFrom.toISOString(),
        dateTo:               row.dateTo.toISOString(),
      }).pipe(catchError(err => of<ImportResult>({
        imported: 0, skipped: 0, errors: 1,
        errorMessages: [err.error?.message ?? 'Import failed'],
      }))).subscribe(result => {
        this.updateRow(row.ctraderAccount.ctidTraderAccountId, r => ({
          ...r, result, importing: false,
          error: result.errors > 0 ? result.errorMessages[0] : null,
        }));

        // Persist cTrader link so auto-sync can run without another login.
        if (result.errors === 0 && this.refreshToken()) {
          const linkReq: CTraderLinkRequest = {
            tradeJAccountId:     row.tradeJAccount!.id,
            ctidTraderAccountId: row.ctraderAccount.ctidTraderAccountId,
            isLive:              row.ctraderAccount.isLive,
            refreshToken:        this.refreshToken(),
          };
          this.ctrader.linkAccount(linkReq).subscribe();
        }

        importNext(idx + 1);
      });
    };

    importNext(0);
  }

  private updateRow(ctidId: number, fn: (r: AccountRow) => AccountRow): void {
    this.accountRows.update(rows => rows.map(r =>
      r.ctraderAccount.ctidTraderAccountId === ctidId ? fn(r) : r
    ));
  }

  private reset(): void {
    this.step.set('idle');
    this.errorMsg.set(null);
    this.accessToken.set('');
    this.refreshToken.set('');
    this.accountRows.set([]);
    window.removeEventListener('message', this.msgHandler);
    this.popup?.close();
  }

  accountLabel(a: Account): string {
    return `${a.name} (${a.broker} · ${a.accountNumber})`;
  }

  formatAccountType(a: CTraderAccount): string {
    return a.isLive ? 'Live' : 'Demo';
  }
}
