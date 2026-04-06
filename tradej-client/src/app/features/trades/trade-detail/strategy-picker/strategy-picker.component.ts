import {
  Component, Input, OnInit, OnChanges, SimpleChanges, inject, signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { StrategyListItem } from '../../../../core/models/strategy.model';
import { StrategyService } from '../../../../core/services/strategy.service';
import { TradeService } from '../../../../core/services/trade.service';

@Component({
  selector: 'app-strategy-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './strategy-picker.component.html',
  styleUrl: './strategy-picker.component.scss'
})
export class StrategyPickerComponent implements OnInit, OnChanges {
  @Input({ required: true }) tradeId!: number;
  @Input({ required: true }) accountId!: number;
  @Input() selectedStrategyIds: number[] = [];

  private strategyService = inject(StrategyService);
  private tradeService = inject(TradeService);
  private changed$ = new Subject<number[]>();
  private destroy$ = new Subject<void>();

  strategies = signal<StrategyListItem[]>([]);
  selected = new Set<number>();
  dropdownOpen = false;

  ngOnInit(): void {
    this.changed$.pipe(
      debounceTime(600),
      takeUntil(this.destroy$)
    ).subscribe(ids => {
      this.tradeService.updateStrategyIds(this.tradeId, ids).subscribe();
    });

    this.strategyService.getAll().subscribe({
      next: items => {
        this.strategies.set(items);
        this.rebuildSelected();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedStrategyIds'] && !changes['selectedStrategyIds'].firstChange) {
      this.rebuildSelected();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  rebuildSelected(): void {
    this.selected = new Set(this.selectedStrategyIds);
  }

  toggle(id: number): void {
    if (this.selected.has(id)) this.selected.delete(id);
    else this.selected.add(id);
    this.changed$.next([...this.selected]);
  }

  isSelected(id: number): boolean {
    return this.selected.has(id);
  }

  get selectedList(): StrategyListItem[] {
    return this.strategies().filter(s => this.selected.has(s.id));
  }

  closeDropdown(): void {
    this.dropdownOpen = false;
  }
}
