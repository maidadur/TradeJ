import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DayNote, DayNotePage, DayTagDef, DayViewData } from '../models/day-view.model';

@Injectable({ providedIn: 'root' })
export class DayViewService {
  private http = inject(HttpClient);
  private readonly apiUrl = '/api/dayview';
  private readonly notesUrl = '/api/daynotes';
  private readonly tagDefsUrl = '/api/daytagdefs';

  getDayView(accountIds: number[], year: number, month?: number) {
    const idParams = accountIds.map(id => `accountIds=${id}`).join('&');
    let url = `${this.apiUrl}?${idParams}&year=${year}`;
    if (month) url += `&month=${month}`;
    return this.http.get<DayViewData>(url);
  }

  getDayViewRange(accountIds: number[], dateFrom: Date, dateTo: Date) {
    const idParams = accountIds.map(id => `accountIds=${id}`).join('&');
    const from = dateFrom.toISOString();
    const to   = dateTo.toISOString();
    return this.http.get<DayViewData>(
      `${this.apiUrl}?${idParams}&dateFrom=${from}&dateTo=${to}`
    );
  }

  saveNote(date: string, content: string) {
    return this.http.put<DayNote>(`${this.notesUrl}/${date}`, { content });
  }

  getAllNotes(page = 1, pageSize = 20) {
    return this.http.get<DayNotePage>(`${this.notesUrl}?page=${page}&pageSize=${pageSize}`);
  }

  addDayTag(date: string, dayTagDefId: number) {
    return this.http.post<void>(`${this.notesUrl}/${date}/tags/${dayTagDefId}`, {});
  }

  removeDayTag(date: string, dayTagDefId: number) {
    return this.http.delete<void>(`${this.notesUrl}/${date}/tags/${dayTagDefId}`);
  }

  // ---- Day tag definitions ----
  getDayTagDefs() {
    return this.http.get<DayTagDef[]>(this.tagDefsUrl);
  }

  createDayTagDef(name: string, color: string) {
    return this.http.post<DayTagDef>(this.tagDefsUrl, { name, color });
  }

  updateDayTagDef(id: number, name: string, color: string) {
    return this.http.put<void>(`${this.tagDefsUrl}/${id}`, { name, color });
  }

  deleteDayTagDef(id: number) {
    return this.http.delete<void>(`${this.tagDefsUrl}/${id}`);
  }
}
