import {
  Component, Input, OnInit, OnChanges, OnDestroy, SimpleChanges, inject, signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { TagCategory, Tag } from '../../../../core/models/tag.model';
import { TagService } from '../../../../core/services/tag.service';
import { TradeService } from '../../../../core/services/trade.service';

@Component({
  selector: 'app-tag-picker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tag-picker.component.html',
  styleUrl: './tag-picker.component.scss'
})
export class TagPickerComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) tradeId!: number;
  @Input({ required: true }) accountId!: number;
  @Input() selectedTagIds: number[] = [];

  private tagService = inject(TagService);
  private tradeService = inject(TradeService);
  private tagsChanged$ = new Subject<number[]>();
  private destroy$ = new Subject<void>();

  categories = signal<TagCategory[]>([]);
  loading = signal(false);

  selectedByCategory = new Map<number, Tag[]>();
  filterText: Record<number, string> = {};
  openDropdownId: number | null = null;
  catMenuOpen: number | null = null;

  showAddCatDialog = false;
  newCatName = '';
  newCatColor = '#6366f1';

  editingCat: TagCategory | null = null;
  editCatName = '';
  editCatColor = '';

  readonly colorOptions = [
    '#6366f1', '#8b5cf6', '#ec4899', '#f43f5e',
    '#f97316', '#f59e0b', '#2FA87A', '#06b6d4'
  ];

  ngOnInit() {
    this.tagsChanged$.pipe(
      debounceTime(600),
      takeUntil(this.destroy$)
    ).subscribe(ids => {
      this.tradeService.updateTagIds(this.tradeId, ids).subscribe();
    });

    this.loadCategories();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['selectedTagIds'] && !changes['selectedTagIds'].firstChange) {
      this.rebuildSelected();
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCategories() {
    this.loading.set(true);
    this.tagService.getCategories().subscribe({
      next: cats => {
        this.categories.set(cats);
        this.rebuildSelected();
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  rebuildSelected() {
    for (const cat of this.categories()) {
      const selected = cat.tags.filter(t => this.selectedTagIds.includes(t.id));
      this.selectedByCategory.set(cat.id, [...selected]);
    }
  }

  getSelected(catId: number): Tag[] {
    return this.selectedByCategory.get(catId) ?? [];
  }

  getFiltered(cat: TagCategory): Tag[] {
    const text = (this.filterText[cat.id] ?? '').toLowerCase().trim();
    const selectedIds = new Set(this.getSelected(cat.id).map(t => t.id));
    return cat.tags.filter(t =>
      !selectedIds.has(t.id) && (!text || t.name.toLowerCase().includes(text))
    );
  }

  canCreate(cat: TagCategory): boolean {
    const text = (this.filterText[cat.id] ?? '').trim();
    if (!text) return false;
    return !cat.tags.some(t => t.name.toLowerCase() === text.toLowerCase());
  }

  selectTag(cat: TagCategory, tag: Tag) {
    const selected = this.getSelected(cat.id);
    if (!selected.find(t => t.id === tag.id)) {
      this.selectedByCategory.set(cat.id, [...selected, tag]);
    }
    this.filterText[cat.id] = '';
    this.openDropdownId = null;
    this.emitChange();
  }

  removeTag(catId: number, tagId: number) {
    this.selectedByCategory.set(catId, this.getSelected(catId).filter(t => t.id !== tagId));
    this.emitChange();
  }

  handleEnter(cat: TagCategory) {
    const text = (this.filterText[cat.id] ?? '').trim();
    if (!text) return;
    const exact = cat.tags.find(t => t.name.toLowerCase() === text.toLowerCase());
    if (exact) {
      this.selectTag(cat, exact);
    } else {
      this.createAndSelect(cat);
    }
  }

  createAndSelect(cat: TagCategory) {
    const name = (this.filterText[cat.id] ?? '').trim();
    if (!name) return;
    this.tagService.createTag(cat.id, name).subscribe(newTag => {
      newTag.usageCount = 0;
      this.categories.update(cats =>
        cats.map(c => c.id === cat.id ? { ...c, tags: [...c.tags, newTag] } : c)
      );
      const selected = this.getSelected(cat.id);
      this.selectedByCategory.set(cat.id, [...selected, newTag]);
      this.filterText[cat.id] = '';
      this.openDropdownId = null;
      this.emitChange();
    });
  }

  openDropdown(catId: number) {
    this.openDropdownId = catId;
    this.catMenuOpen = null;
  }

  scheduleClose(catId: number) {
    setTimeout(() => {
      if (this.openDropdownId === catId) this.openDropdownId = null;
    }, 200);
  }

  toggleCatMenu(catId: number) {
    this.catMenuOpen = this.catMenuOpen === catId ? null : catId;
    this.openDropdownId = null;
  }

  openAddCategory() {
    this.newCatName = '';
    this.newCatColor = '#6366f1';
    this.showAddCatDialog = true;
    this.catMenuOpen = null;
  }

  addCategory() {
    const name = this.newCatName.trim();
    if (!name) return;
    this.tagService.createCategory(name, this.newCatColor).subscribe(cat => {
      cat.tags = cat.tags ?? [];
      this.categories.update(cats => [...cats, cat]);
      this.selectedByCategory.set(cat.id, []);
      this.showAddCatDialog = false;
    });
  }

  startEditCategory(cat: TagCategory) {
    this.editingCat = cat;
    this.editCatName = cat.name;
    this.editCatColor = cat.color;
    this.catMenuOpen = null;
  }

  saveEditCategory() {
    const cat = this.editingCat;
    if (!cat || !this.editCatName.trim()) return;
    const name = this.editCatName.trim();
    const color = this.editCatColor;
    this.tagService.updateCategory(cat.id, name, color, cat.sortOrder).subscribe(() => {
      this.categories.update(cats =>
        cats.map(c => c.id === cat.id ? { ...c, name, color } : c)
      );
      this.editingCat = null;
    });
  }

  deleteCategory(cat: TagCategory) {
    if (!confirm(`Delete category "${cat.name}"? All its tags will be removed from trades.`)) return;
    this.tagService.deleteCategory(cat.id).subscribe(() => {
      this.categories.update(cats => cats.filter(c => c.id !== cat.id));
      this.selectedByCategory.delete(cat.id);
      this.catMenuOpen = null;
      this.emitChange();
    });
  }

  deleteTagOption(cat: TagCategory, tagId: number, event: MouseEvent) {
    event.stopPropagation();
    event.preventDefault();
    this.tagService.deleteTag(cat.id, tagId).subscribe(() => {
      this.categories.update(cats =>
        cats.map(c => c.id === cat.id ? { ...c, tags: c.tags.filter(t => t.id !== tagId) } : c)
      );
      const selected = this.getSelected(cat.id);
      if (selected.some(t => t.id === tagId)) {
        this.selectedByCategory.set(cat.id, selected.filter(t => t.id !== tagId));
        this.emitChange();
      }
    });
  }

  emitChange() {
    const all: number[] = [];
    this.selectedByCategory.forEach(tags => tags.forEach(t => all.push(t.id)));
    this.tagsChanged$.next(all);
  }
}
