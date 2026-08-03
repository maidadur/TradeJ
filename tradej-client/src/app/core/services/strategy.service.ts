import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  ChecklistItem, CreateStrategyDto, StrategyDetail, StrategyListItem, StrategyNote, UpdateStrategyDto
} from '../models/strategy.model';

@Injectable({ providedIn: 'root' })
export class StrategyService {
  private http = inject(HttpClient);
  private readonly base = '/api/strategies';
  private readonly notesBase = '/api/strategynotes';
  private readonly checklistBase = '/api/checklistitems';

  getAll() {
    return this.http.get<StrategyListItem[]>(this.base);
  }

  getById(id: number) {
    return this.http.get<StrategyDetail>(`${this.base}/${id}`);
  }

  create(dto: CreateStrategyDto) {
    return this.http.post<StrategyDetail>(this.base, dto);
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

  createChecklistItem(strategyId: number, text: string) {
    return this.http.post<ChecklistItem>(
      `${this.checklistBase}?strategyId=${strategyId}`,
      { text }
    );
  }

  updateChecklistItem(item: ChecklistItem) {
    return this.http.put<ChecklistItem>(`${this.checklistBase}/${item.id}`, item);
  }

  deleteChecklistItem(id: number) {
    return this.http.delete<void>(`${this.checklistBase}/${id}`);
  }

  reorderChecklistItems(strategyId: number, orderedIds: number[]) {
    return this.http.post<void>(
      `${this.checklistBase}/reorder?strategyId=${strategyId}`,
      { orderedIds }
    );
  }

  resetChecklist(strategyId: number) {
    return this.http.post<void>(`${this.checklistBase}/reset?strategyId=${strategyId}`, {});
  }
}
