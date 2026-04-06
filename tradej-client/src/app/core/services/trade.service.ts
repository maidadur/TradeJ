import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { PagedResult, Trade, TradeFilter } from '../models/trade.model';

@Injectable({ providedIn: 'root' })
export class TradeService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/trades';

  getTrades(filter: TradeFilter) {
    let params = new HttpParams()
      .set('page', filter.page ?? 1)
      .set('pageSize', filter.pageSize ?? 50);
    for (const id of filter.accountIds) {
      params = params.append('accountIds', id);
    }

    if (filter.symbol) params = params.set('symbol', filter.symbol);
    if (filter.direction) params = params.set('direction', filter.direction);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.dateFrom) params = params.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) params = params.set('dateTo', filter.dateTo);
    if (filter.sortBy) params = params.set('sortBy', filter.sortBy);
    if (filter.sortDesc !== undefined) params = params.set('sortDesc', filter.sortDesc);

    return this.http.get<PagedResult<Trade>>(this.apiUrl, { params });
  }

  getTrade(id: number) {
    return this.http.get<Trade>(`${this.apiUrl}/${id}`);
  }

  updateNotes(id: number, notes: string | null) {
    return this.http.patch<void>(`${this.apiUrl}/${id}/notes`, { notes, tags: null });
  }

  updateTagIds(id: number, tagIds: number[]) {
    return this.http.patch<void>(`${this.apiUrl}/${id}/tags`, { tagIds });
  }

  updateStrategyIds(id: number, strategyIds: number[]) {
    return this.http.patch<void>(`${this.apiUrl}/${id}/strategies`, { strategyIds });
  }
}
