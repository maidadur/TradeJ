import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Dashboard } from '../models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/dashboard';

  getDashboard(accountIds: number[], year: number, month?: number) {
    const idParams = accountIds.map(id => `accountIds=${id}`).join('&');
    let url = `${this.apiUrl}?${idParams}&year=${year}`;
    if (month) url += `&month=${month}`;
    return this.http.get<Dashboard>(url);
  }
}
