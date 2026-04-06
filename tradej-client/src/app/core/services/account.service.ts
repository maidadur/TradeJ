import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, combineLatest, tap } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';
import { Account, CreateAccountDto, UpdateAccountDto } from '../models/account.model';

const SELECTED_ACCOUNT_IDS_KEY = 'tradej_selected_account_ids';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/accounts';

  private _allAccounts = new BehaviorSubject<Account[]>([]);
  allAccounts$ = this._allAccounts.asObservable();

  private _selectedAccountIds = new BehaviorSubject<number[]>(this.loadStoredIds());
  selectedAccountIds$ = this._selectedAccountIds.asObservable();

  /** Backward-compat: emits the first selected account (for note-saving, currency display, etc.) */
  readonly selectedAccount$ = combineLatest([
    this._selectedAccountIds,
    this._allAccounts,
  ]).pipe(
    map(([ids, accounts]) =>
      ids.length > 0 ? (accounts.find(a => a.id === ids[0]) ?? null) : null
    ),
    distinctUntilChanged((a, b) => a?.id === b?.id)
  );

  get selectedAccountIds(): number[] { return this._selectedAccountIds.value; }
  get allAccounts(): Account[]        { return this._allAccounts.value; }

  selectAccountIds(ids: number[]): void {
    const cur = this._selectedAccountIds.value;
    if (JSON.stringify(cur) === JSON.stringify(ids)) return;
    this._selectedAccountIds.next(ids);
    localStorage.setItem(SELECTED_ACCOUNT_IDS_KEY, JSON.stringify(ids));
  }

  getAll() {
    return this.http.get<Account[]>(this.apiUrl).pipe(
      tap(accounts => {
        this._allAccounts.next(accounts);
        const stored = this.loadStoredIds();
        const valid  = stored.filter(id => accounts.some(a => a.id === id));
        const toSelect = valid.length > 0 ? valid : accounts.map(a => a.id);
        this.selectAccountIds(toSelect);
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

  private loadStoredIds(): number[] {
    try {
      const raw = localStorage.getItem(SELECTED_ACCOUNT_IDS_KEY);
      if (raw) return JSON.parse(raw);
      // Migrate from old single-account key
      const oldRaw = localStorage.getItem('tradej_selected_account');
      if (oldRaw) {
        const old = JSON.parse(oldRaw) as { id: number };
        if (old?.id) return [old.id];
      }
      return [];
    } catch { return []; }
  }
}
