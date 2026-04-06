import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EditorModule } from 'primeng/editor';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { Subject, debounceTime, mergeMap, map } from 'rxjs';
import { DayViewService } from '../../core/services/day-view.service';
import { AccountService } from '../../core/services/account.service';
import { DayNote, DayTagDef } from '../../core/models/day-view.model';

@Component({
  selector: 'app-notebook',
  standalone: true,
  imports: [CommonModule, FormsModule, EditorModule, ButtonModule, SelectModule, TooltipModule],
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

  // ---- Filters ----
  selectedYear = signal<number>(new Date().getFullYear());
  selectedMonth = signal<number | null>(null);

  yearOptions = computed(() => {
    const years = new Set<number>();
    years.add(new Date().getFullYear());
    for (const note of this.allNotes()) {
      years.add(+note.date.slice(0, 4));
    }
    return [...years].sort((a, b) => b - a);
  });

  monthOptions = [
    { label: 'All months', value: null },
    ...Array.from({ length: 12 }, (_, i) => ({
      label: new Date(2000, i).toLocaleString('default', { month: 'long' }),
      value: i + 1
    }))
  ];

  notes = computed(() => {
    const year = this.selectedYear();
    const month = this.selectedMonth();
    return this.allNotes().filter(n => {
      const d = new Date(n.date + 'T00:00:00');
      if (d.getFullYear() !== year) return false;
      if (month !== null && d.getMonth() + 1 !== month) return false;
      return true;
    });
  });

  // ---- Note expand / edit ----
  expandedDate: string | null = null;
  editingContent = '';
  savingNotes = signal<Set<string>>(new Set());
  private noteSave$ = new Subject<{ date: string; content: string }>();

  // ---- Tags ----
  dayTagDefs = signal<DayTagDef[]>([]);
  openTagPickerDate: string | null = null;
  tagFilterText: Record<string, string> = {};

  tagMap = computed(() => {
    const m = new Map<number, DayTagDef>();
    for (const t of this.dayTagDefs()) m.set(t.id, t);
    return m;
  });

  ngOnInit(): void {
    this.noteSave$.pipe(
      debounceTime(1500),
      mergeMap(({ date, content }) => {
        return this.dayViewService.saveNote(date, content).pipe(
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
      if (account) {
        this.load();
        this.dayViewService.getDayTagDefs().subscribe(defs => this.dayTagDefs.set(defs));
      }
    });
  }

  load(): void {
    const id = this.accountId();
    if (!id) return;
    this.loading.set(true);
    this.dayViewService.getAllNotes(1, 500).subscribe({
      next: result => {
        this.allNotes.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onFilterChange(): void {
    this.expandedDate = null;
    this.openTagPickerDate = null;
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

  // ---- Tag picker ----
  toggleTagPicker(date: string, event: Event): void {
    event.stopPropagation();
    this.openTagPickerDate = this.openTagPickerDate === date ? null : date;
  }

  closeTagPicker(): void {
    this.openTagPickerDate = null;
    this.tagFilterText = {};
  }

  getTagsForNote(note: DayNote): DayTagDef[] {
    const m = this.tagMap();
    return note.tagIds.map(id => m.get(id)).filter((t): t is DayTagDef => !!t);
  }

  isTagSelected(note: DayNote, tagId: number): boolean {
    return note.tagIds.includes(tagId);
  }

  toggleTag(note: DayNote, tag: DayTagDef): void {
    const selected = this.isTagSelected(note, tag.id);
    if (selected) {
      this.allNotes.update(list =>
        list.map(n => n.date === note.date ? { ...n, tagIds: n.tagIds.filter(id => id !== tag.id) } : n)
      );
      this.dayViewService.removeDayTag(note.date, tag.id).subscribe();
    } else {
      this.allNotes.update(list =>
        list.map(n => n.date === note.date ? { ...n, tagIds: [...n.tagIds, tag.id] } : n)
      );
      this.dayViewService.addDayTag(note.date, tag.id).subscribe();
    }
  }

  getFilteredTags(note: DayNote): DayTagDef[] {
    const text = (this.tagFilterText[note.date] ?? '').toLowerCase().trim();
    return this.dayTagDefs().filter(t => !text || t.name.toLowerCase().includes(text));
  }

  newTagName = '';
  newTagColor = '#6366f1';

  createAndSelectTag(note: DayNote): void {
    const name = this.newTagName.trim();
    if (!name) return;
    this.dayViewService.createDayTagDef(name, this.newTagColor).subscribe(def => {
      this.dayTagDefs.update(defs => [...defs, def]);
      this.allNotes.update(list =>
        list.map(n => n.date === note.date ? { ...n, tagIds: [...n.tagIds, def.id] } : n)
      );
      this.dayViewService.addDayTag(note.date, def.id).subscribe();
      this.newTagName = '';
      this.newTagColor = '#6366f1';
    });
  }

  // ---- Formatting ----
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
