import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { TabsModule } from 'primeng/tabs';
import { EditorModule } from 'primeng/editor';
import { StrategyService } from '../../../core/services/strategy.service';
import { StrategyDetail, StrategyNote, UpdateStrategyDto } from '../../../core/models/strategy.model';

type ActiveTab = 'stats' | 'trades' | 'notes';

@Component({
  selector: 'app-strategy-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    ButtonModule, DialogModule, InputTextModule, TextareaModule,
    TabsModule, EditorModule
  ],
  templateUrl: './strategy-detail.component.html',
  styleUrl: './strategy-detail.component.scss'
})
export class StrategyDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private strategyService = inject(StrategyService);

  strategy = signal<StrategyDetail | null>(null);
  loading = signal(false);
  activeTab: ActiveTab = 'stats';

  // Edit name/description
  showEditDialog = false;
  editName = '';
  editDescription = '';
  saving = false;

  // Note dialogs
  showAddNoteDialog = false;
  noteTitle = '';
  noteContent = '';
  savingNote = false;

  editingNote: StrategyNote | null = null;
  showEditNoteDialog = false;
  editNoteTitle = '';
  editNoteContent = '';

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.load(id);
  }

  load(id: number): void {
    this.loading.set(true);
    this.strategyService.getById(id).subscribe({
      next: s => { this.strategy.set(s); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  openEditDialog(): void {
    const s = this.strategy();
    if (!s) return;
    this.editName = s.name;
    this.editDescription = s.description ?? '';
    this.showEditDialog = true;
  }

  saveEdit(): void {
    const s = this.strategy();
    if (!s || !this.editName.trim()) return;
    this.saving = true;
    const dto: UpdateStrategyDto = {
      name: this.editName.trim(),
      description: this.editDescription.trim() || undefined
    };
    this.strategyService.update(s.id, dto).subscribe({
      next: updated => {
        this.strategy.update(cur => cur ? { ...cur, name: updated.name, description: updated.description } : cur);
        this.showEditDialog = false;
        this.saving = false;
      },
      error: () => { this.saving = false; }
    });
  }

  onImageChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const id = this.strategy()?.id;
    if (!id) return;
    this.strategyService.uploadImage(id, file).subscribe({
      next: res => {
        this.strategy.update(s => s ? { ...s, imageUrl: res.imageUrl } : s);
      }
    });
    // reset so same file can be re-selected
    input.value = '';
  }

  openAddNote(): void {
    this.noteTitle = '';
    this.noteContent = '';
    this.showAddNoteDialog = true;
  }

  addNote(): void {
    const id = this.strategy()?.id;
    if (!id || !this.noteTitle.trim()) return;
    this.savingNote = true;
    this.strategyService.createNote(id, this.noteTitle.trim(), this.noteContent).subscribe({
      next: note => {
        this.strategy.update(s => s ? { ...s, notes: [...s.notes, note] } : s);
        this.showAddNoteDialog = false;
        this.savingNote = false;
      },
      error: () => { this.savingNote = false; }
    });
  }

  openEditNote(note: StrategyNote): void {
    this.editingNote = note;
    this.editNoteTitle = note.title;
    this.editNoteContent = note.content;
    this.showEditNoteDialog = true;
  }

  saveEditNote(): void {
    if (!this.editingNote || !this.editNoteTitle.trim()) return;
    this.savingNote = true;
    this.strategyService.updateNote(this.editingNote.id, this.editNoteTitle.trim(), this.editNoteContent).subscribe({
      next: updated => {
        this.strategy.update(s => s ? {
          ...s,
          notes: s.notes.map(n => n.id === updated.id ? updated : n)
        } : s);
        this.showEditNoteDialog = false;
        this.editingNote = null;
        this.savingNote = false;
      },
      error: () => { this.savingNote = false; }
    });
  }

  deleteNote(note: StrategyNote): void {
    if (!confirm(`Delete note "${note.title}"?`)) return;
    this.strategyService.deleteNote(note.id).subscribe({
      next: () => {
        this.strategy.update(s => s ? { ...s, notes: s.notes.filter(n => n.id !== note.id) } : s);
      }
    });
  }

  formatPnL(v: number): string {
    return (v >= 0 ? '+' : '') + v.toFixed(2);
  }

  pnlClass(v: number): string {
    if (v > 0) return 'pnl-pos';
    if (v < 0) return 'pnl-neg';
    return '';
  }

  formatDuration(minutes: number): string {
    if (minutes < 60) return `${Math.round(minutes)}m`;
    const h = Math.floor(minutes / 60);
    const m = Math.round(minutes % 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }
}
