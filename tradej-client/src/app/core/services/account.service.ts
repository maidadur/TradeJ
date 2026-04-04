import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, tap } from 'rxjs';
import { Account, CreateAccountDto, UpdateAccountDto } from '../models/account.model';

const SELECTED_ACCOUNT_KEY = 'tradej_selected_account';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/accounts';

  private _selectedAccount = new BehaviorSubject<Account | null>(
    this.loadStoredAccount()
  );
  selectedAccount$ = this._selectedAccount.asObservable();

  get selectedAccount(): Account | null {
    return this._selectedAccount.value;
  }

  selectAccount(account: Account | null): void {
    this._selectedAccount.next(account);
    if (account) localStorage.setItem(SELECTED_ACCOUNT_KEY, JSON.stringify(account));
    else localStorage.removeItem(SELECTED_ACCOUNT_KEY);
  }

  getAll() {
    return this.http.get<Account[]>(this.apiUrl).pipe(
      tap(accounts => {
        // Auto-select first account if none selected
        if (!this._selectedAccount.value && accounts.length > 0)
          this.selectAccount(accounts[0]);
        // Refresh stored account data if it was selected
        const stored = this._selectedAccount.value;
        if (stored) {
          const refreshed = accounts.find(a => a.id === stored.id);
          if (refreshed) this.selectAccount(refreshed);
        }
      })
    );
  }

  create(dto: CreateAccountDto) {
    return this.http.post<Account>(this.apiUrl, dto);
  }

  update(id: number, dto: UpdateAccountDto) {
    return this.http.put<void>(`${this.apiUrl}/${id}`, dto);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  private loadStoredAccount(): Account | null {
    try {
      const raw = localStorage.getItem(SELECTED_ACCOUNT_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  }
}
