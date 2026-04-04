import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DayNote, DayNotePage, DayViewData } from '../models/day-view.model';

@Injectable({ providedIn: 'root' })
export class DayViewService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/dayview';
  private readonly notesUrl = '/api/daynotes';

  getDayView(accountId: number, year: number, month?: number) {
    let url = `${this.apiUrl}?accountId=${accountId}&year=${year}`;
    if (month) url += `&month=${month}`;
    return this.http.get<DayViewData>(url);
  }

  getDayViewRange(accountId: number, dateFrom: Date, dateTo: Date) {
    const from = dateFrom.toISOString();
    const to   = dateTo.toISOString();
    return this.http.get<DayViewData>(
      `${this.apiUrl}?accountId=${accountId}&dateFrom=${from}&dateTo=${to}`
    );
  }

  saveNote(accountId: number, date: string, content: string) {
    return this.http.put<DayNote>(
      `${this.notesUrl}/${date}?accountId=${accountId}`,
      { content }
    );
  }

  getAllNotes(accountId: number, page = 1, pageSize = 20) {
    return this.http.get<DayNotePage>(
      `${this.notesUrl}?accountId=${accountId}&page=${page}&pageSize=${pageSize}`
    );
  }
}
