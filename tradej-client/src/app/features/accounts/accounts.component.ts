import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { Account, CreateAccountDto, UpdateAccountDto } from '../../core/models/account.model';
import { AccountService } from '../../core/services/account.service';

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, InputTextModule,
    SelectModule, TableModule, DialogModule, TagModule,
    ToastModule, ConfirmDialogModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './accounts.component.html',
  styleUrl: './accounts.component.scss'
})
export class AccountsComponent implements OnInit {
  private accountService = inject(AccountService);
  private messageService = inject(MessageService);
  private confirmService = inject(ConfirmationService);

  accounts = signal<Account[]>([]);
  loading = signal(false);
  saving = signal(false);
  showDialog = false;
  editingAccount: Account | null = null;

  form: any = { name: '', broker: 'MT5', accountNumber: '', currency: 'USD',
    initialBalance: 0, mt5Server: '', mt5InvestorPassword: '', metaApiAccountId: '', metaApiToken: '', metaApiRegion: 'london' };
  formIsActive = true;

  brokerOptions = ['MT5', 'cTrader', 'ByBit'];
  currencyOptions = ['USD', 'EUR', 'GBP', 'JPY', 'BTC', 'USDT'];
  statusOptions = [
    { label: 'Active', value: true },
    { label: 'Inactive', value: false }
  ];
  regionOptions = [
    { label: 'London', value: 'london' },
    { label: 'New York', value: 'new-york' },
    { label: 'Singapore', value: 'singapore' },
    { label: 'Sydney', value: 'sydney' },
    { label: 'Hong Kong', value: 'hong-kong' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.accountService.getAll().subscribe({
      next: list => { this.accounts.set(list); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  openCreateDialog(): void {
    this.editingAccount = null;
    this.form = { name: '', broker: 'MT5', accountNumber: '', currency: 'USD',
      initialBalance: 0, mt5Server: '', mt5InvestorPassword: '', metaApiAccountId: '', metaApiToken: '', metaApiRegion: 'london' };
    this.formIsActive = true;
    this.showDialog = true;
  }

  openEditDialog(account: Account): void {
    this.editingAccount = account;
    this.form = {
      name: account.name,
      broker: account.broker,
      accountNumber: account.accountNumber,
      currency: account.currency,
      initialBalance: account.initialBalance ?? 0,
      mt5Server: account.mt5Server ?? '',
      mt5InvestorPassword: '',   // never pre-fill for security
      metaApiAccountId: account.metaApiAccountId ?? '',
      metaApiToken: '',   // never pre-fill token for security
      metaApiRegion: account.metaApiRegion ?? 'london'
    };
    this.formIsActive = account.isActive;
    this.showDialog = true;
  }

  saveAccount(): void {
    this.saving.set(true);
    if (this.editingAccount) {
      const dto: UpdateAccountDto = {
        name: this.form.name,
        accountNumber: this.form.accountNumber,
        currency: this.form.currency,
        initialBalance: +this.form.initialBalance || 0,
        isActive: this.formIsActive,
        mt5Server: this.form.mt5Server || null,
        mt5InvestorPassword: this.form.mt5InvestorPassword || null,
        metaApiAccountId: this.form.metaApiAccountId || null,
        metaApiToken: this.form.metaApiToken || null,  // null = don't change; value = update
        metaApiRegion: this.form.metaApiRegion || 'london'
      };
      this.accountService.update(this.editingAccount.id, dto).subscribe({
        next: () => {
          this.showDialog = false;
          this.saving.set(false);
          this.load();
          this.messageService.add({ severity: 'success', summary: 'Account updated', life: 2500 });
        },
        error: () => this.saving.set(false)
      });
    } else {
      const dto: CreateAccountDto = {
        name: this.form.name,
        broker: this.form.broker,
        accountNumber: this.form.accountNumber,
        currency: this.form.currency,
        initialBalance: +this.form.initialBalance || 0,
        mt5Server: this.form.mt5Server || null,
        mt5InvestorPassword: this.form.mt5InvestorPassword || null,
        metaApiAccountId: this.form.metaApiAccountId || null,
        metaApiToken: this.form.metaApiToken || null,
        metaApiRegion: this.form.metaApiRegion || 'london'
      };
      this.accountService.create(dto).subscribe({
        next: () => {
          this.showDialog = false;
          this.saving.set(false);
          this.load();
          this.messageService.add({ severity: 'success', summary: 'Account created', life: 2500 });
        },
        error: () => this.saving.set(false)
      });
    }
  }

  confirmDelete(account: Account): void {
    this.confirmService.confirm({
      message: `Delete account "${account.name}"? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-trash',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.accountService.delete(account.id).subscribe({
          next: () => { this.load(); this.messageService.add({ severity: 'success', summary: 'Account deleted', life: 2500 }); },
          error: (err) => {
            const msg = err.error?.message ?? 'Delete failed.';
            this.messageService.add({ severity: 'error', summary: msg, life: 4000 });
          }
        });
      }
    });
  }
}
