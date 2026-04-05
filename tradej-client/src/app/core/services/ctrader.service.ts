import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ImportResult } from './import.service';

export interface CTraderAccount {
  ctidTraderAccountId: number;
  isLive: boolean;
  traderLogin: number;
  brokerName: string;
}

export interface CTraderAccountsResponse {
  accessToken: string;
  refreshToken: string;
  accounts: CTraderAccount[];
}

export interface CTraderImportRequest {
  accessToken: string;
  ctidTraderAccountId: number;
  isLive: boolean;
  tradeJAccountId: number;
  dateFrom: string;
  dateTo: string;
}

export interface CTraderLinkRequest {
  tradeJAccountId: number;
  ctidTraderAccountId: number;
  isLive: boolean;
  refreshToken: string;
}

@Injectable({ providedIn: 'root' })
export class CTraderService {
  private http = inject(HttpClient);
  private readonly base = '/api/ctrader';

  getOAuthUrl(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.base}/oauth-url`);
  }

  exchangeCode(code: string): Observable<CTraderAccountsResponse> {
    return this.http.post<CTraderAccountsResponse>(`${this.base}/accounts`, { code });
  }

  importAccount(req: CTraderImportRequest): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.base}/import`, req);
  }

  linkAccount(req: CTraderLinkRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/link`, req);
  }
}
