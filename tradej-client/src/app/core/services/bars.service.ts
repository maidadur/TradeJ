import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Bar, Timeframe } from '../models/bar.model';

@Injectable({ providedIn: 'root' })
export class BarsService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/bars';

  getBars(accountId: number, symbol: string, timeframe: Timeframe, from: string, to: string) {
    const params = new HttpParams()
      .set('accountId', accountId)
      .set('symbol', symbol)
      .set('timeframe', timeframe)
      .set('from', from)
      .set('to', to);

    return this.http.get<Bar[]>(this.apiUrl, { params });
  }
}
