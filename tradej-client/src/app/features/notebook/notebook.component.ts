import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EditorModule } from 'primeng/editor';
import { ButtonModule } from 'primeng/button';
import { Subject, debounceTime, mergeMap, map } from 'rxjs';
import { DayViewService } from '../../core/services/day-view.service';
import { AccountService } from '../../core/services/account.service';
import { DayNote } from '../../core/models/day-view.model';

@Component({
  selector: 'app-notebook',
  standalone: true,
  imports: [CommonModule, FormsModule, EditorModule, ButtonModule],
  templateUrl: './notebook.component.html',
  styleUrl: './notebook.component.scss'
})
export class NotebookComponent implements OnInit {
  private dayViewService = inject(DayViewService);
  private accountService = inject(AccountService);

  allNotes = signal<DayNote[]>([]);
  total = signal(0);
  loading = signal(false);
  accountId = signal<number | null>(null);

  selectedMonth = signal<string | null>(null);

  expandedDate: string | null = null;
  editingContent = '';
  savingNotes = signal<Set<string>>(new Set());
  private noteSave$ = new Subject<{ date: string; content: string }>();

  availableMonths = computed(() => {
    const months = new Set<string>();
    for (const note of this.allNotes()) {
      months.add(note.date.slice(0, 7));
    }
    return [...months].sort((a, b) => b.localeCompare(a));
  });

  notes = computed(() => {
    const month = this.selectedMonth();
    if (!month) return this.allNotes();
    return this.allNotes().filter(n => n.date.startsWith(month));
  });

  ngOnInit(): void {
    this.noteSave$.pipe(
      debounceTime(1500),
      mergeMap(({ date, content }) => {
        const accountId = this.accountId();
        if (!accountId) return [];
        return this.dayViewService.saveNote(accountId, date, content).pipe(
          map(saved => ({ date, saved }))
        );
      })
    ).subscribe(({ date, saved }) => {
      this.allNotes.update(list =>
        list.map(n => n.date === saved.date ? { ...n, content: saved.content, updatedAt: saved.updatedAt } : n)
      );
      this.savingNotes.update(s => { const n = new Set(s); n.delete(date); return n; });
    });

    this.accountService.selectedAccount$.subscribe(account => {
      this.accountId.set(account?.id ?? null);
      if (account) this.load();
    });
  }

  load(): void {
    const id = this.accountId();
    if (!id) return;
    this.loading.set(true);
    this.dayViewService.getAllNotes(id, 1, 500).subscribe({
      next: result => {
        this.allNotes.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  selectMonth(month: string | null): void {
    this.selectedMonth.set(month);
    this.expandedDate = null;
  }

  formatMonthLabel(ym: string): string {
    const [year, month] = ym.split('-');
    const d = new Date(+year, +month - 1, 1);
    return d.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
  }

  toggleNote(date: string, content: string): void {
    if (this.expandedDate === date) {
      this.expandedDate = null;
    } else {
      this.expandedDate = date;
      this.editingContent = content;
    }
  }

  onContentChange(date: string, content: string): void {
    this.editingContent = content;
    this.savingNotes.update(s => new Set([...s, date]));
    this.noteSave$.next({ date, content });
  }

  isSaving(date: string): boolean {
    return this.savingNotes().has(date);
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr + 'T00:00:00');
    return d.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
  }

  formatUpdated(dt: string): string {
    return new Date(dt).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: '2-digit', minute: '2-digit', hour12: false
    });
  }

  stripHtml(html: string): string {
    const tmp = document.createElement('div');
    tmp.innerHTML = html;
    return tmp.textContent || tmp.innerText || '';
  }
}
