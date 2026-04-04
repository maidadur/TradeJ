import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface ImportResult {
  imported: number;
  skipped: number;
  errors: number;
  errorMessages: string[];
}

@Injectable({ providedIn: 'root' })
export class ImportService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/import';

  import(broker: 'mt5' | 'ctrader' | 'bybit', accountId: number, file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ImportResult>(
      `${this.apiUrl}/${broker}?accountId=${accountId}`,
      form
    );
  }

  importLive(accountId: number, dateFrom: Date, dateTo: Date) {
    return this.http.post<ImportResult>(
      `${this.apiUrl}/mt5-live?accountId=${accountId}`,
      {
        dateFrom: dateFrom.toISOString(),
        dateTo: dateTo.toISOString()
      }
    );
  }

  importSync(accountId: number, dateFrom: Date, dateTo: Date) {
    return this.http.post<ImportResult>(
      `${this.apiUrl}/mt5-sync?accountId=${accountId}`,
      {
        dateFrom: dateFrom.toISOString(),
        dateTo: dateTo.toISOString()
      }
    );
  }
}
