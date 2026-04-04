import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  CreateStrategyDto, StrategyDetail, StrategyListItem, StrategyNote, UpdateStrategyDto
} from '../models/strategy.model';

@Injectable({ providedIn: 'root' })
export class StrategyService {
  private http = inject(HttpClient);
  private readonly base = '/api/strategies';
  private readonly notesBase = '/api/strategynotes';

  getAll(accountId: number) {
    return this.http.get<StrategyListItem[]>(`${this.base}?accountId=${accountId}`);
  }

  getById(id: number) {
    return this.http.get<StrategyDetail>(`${this.base}/${id}`);
  }

  create(accountId: number, dto: CreateStrategyDto) {
    return this.http.post<StrategyDetail>(`${this.base}?accountId=${accountId}`, dto);
  }

  update(id: number, dto: UpdateStrategyDto) {
    return this.http.put<StrategyDetail>(`${this.base}/${id}`, dto);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  uploadImage(id: number, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ imageUrl: string }>(`${this.base}/${id}/image`, form);
  }

  createNote(strategyId: number, title: string, content: string) {
    return this.http.post<StrategyNote>(
      `${this.notesBase}?strategyId=${strategyId}`,
      { title, content }
    );
  }

  updateNote(id: number, title: string, content: string) {
    return this.http.put<StrategyNote>(`${this.notesBase}/${id}`, { title, content });
  }

  deleteNote(id: number) {
    return this.http.delete<void>(`${this.notesBase}/${id}`);
  }
}
