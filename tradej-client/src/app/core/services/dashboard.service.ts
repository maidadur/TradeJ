import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Dashboard } from '../models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/dashboard';

  getDashboard(accountId: number, year: number, month?: number) {
    let url = `${this.apiUrl}?accountId=${accountId}&year=${year}`;
    if (month) url += `&month=${month}`;
    return this.http.get<Dashboard>(url);
  }
}
