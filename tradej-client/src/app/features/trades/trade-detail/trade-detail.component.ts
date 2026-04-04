import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { Trade } from '../../../core/models/trade.model';
import { TradeService } from '../../../core/services/trade.service';
import { TagPickerComponent } from './tag-picker/tag-picker.component';
import { StrategyPickerComponent } from './strategy-picker/strategy-picker.component';

@Component({
  selector: 'app-trade-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    ButtonModule, TagModule, EditorModule, InputTextModule,
    ToastModule, TagPickerComponent, StrategyPickerComponent
  ],
  providers: [MessageService],
  templateUrl: './trade-detail.component.html',
  styleUrl: './trade-detail.component.scss'
})
export class TradeDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private tradeService = inject(TradeService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();
  private notesChanged$ = new Subject<string>();

  trade = signal<Trade | null>(null);
  loading = signal(false);
  saving = signal(false);

  notesHtml = '';

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadTrade(id);

    // Auto-save notes with debounce
    this.notesChanged$.pipe(
      debounceTime(1200),
      takeUntil(this.destroy$)
    ).subscribe(html => {
      this.saveNotes(html);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadTrade(id: number): void {
    this.loading.set(true);
    this.tradeService.getTrade(id).subscribe({
      next: trade => {
        this.trade.set(trade);
        this.notesHtml = trade.notes ?? '';
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onNotesChange(html: string): void {
    this.notesChanged$.next(html);
  }

  saveNotes(html: string): void {
    const t = this.trade();
    if (!t) return;
    this.saving.set(true);
    this.tradeService.updateNotes(t.id, html).subscribe({
      next: () => { this.saving.set(false); },
      error: () => this.saving.set(false)
    });
  }

  formatPnL(value: number): string {
    const abs = Math.abs(value);
    return (value < 0 ? '-$' : '$') + abs.toFixed(2);
  }

  pnlClass(value: number): string {
    if (value > 0) return 'pnl-pos';
    if (value < 0) return 'pnl-neg';
    return 'pnl-zero';
  }
}
