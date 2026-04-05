import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export const SETTING_MT5_SYNC   = 'AutoSync.Mt5Enabled';
export const SETTING_CTRADER_SYNC = 'AutoSync.CTraderEnabled';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/settings';

  getAll(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(this.apiUrl);
  }

  update(key: string, value: boolean): Observable<void> {
    return this.http.patch<void>(
      `${this.apiUrl}/${encodeURIComponent(key)}`,
      JSON.stringify(value ? 'true' : 'false'),
      { headers: { 'Content-Type': 'application/json' } }
    );
  }
}
