import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { Trade } from '../../../core/models/trade.model';
import { TradeService } from '../../../core/services/trade.service';
import { TradeNavigationService } from '../../../core/services/trade-navigation.service';
import { TagPickerComponent } from './tag-picker/tag-picker.component';
import { StrategyPickerComponent } from './strategy-picker/strategy-picker.component';

@Component({
  selector: 'app-trade-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    ButtonModule, TagModule, EditorModule, InputTextModule, InputNumberModule,
    ToastModule, TagPickerComponent, StrategyPickerComponent
  ],
  providers: [MessageService],
  templateUrl: './trade-detail.component.html',
  styleUrl: './trade-detail.component.scss'
})
export class TradeDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private tradeService = inject(TradeService);
  private tradeNavService = inject(TradeNavigationService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();
  private notesChanged$ = new Subject<string>();

  trade = signal<Trade | null>(null);
  loading = signal(false);
  saving = signal(false);

  prevId = computed(() => {
    const t = this.trade();
    return t ? this.tradeNavService.getPrevId(t.id) : null;
  });

  nextId = computed(() => {
    const t = this.trade();
    return t ? this.tradeNavService.getNextId(t.id) : null;
  });

  navPosition = computed(() => {
    const t = this.trade();
    return t ? this.tradeNavService.getPosition(t.id) : null;
  });

  notesHtml = '';

  // Metrics editing state
  metricsRR: number | null = null;
  metricsActualRR: number | null = null;
  metricsRiskPercent: number | null = null;
  savingMetrics = signal(false);

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = Number(params.get('id'));
      this.loadTrade(id);
    });

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
        this.metricsRR = trade.rr ?? null;
        this.metricsActualRR = trade.actualRR ?? null;
        this.metricsRiskPercent = trade.riskPercent ?? null;
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

  saveMetrics(): void {
    const t = this.trade();
    if (!t) return;
    this.savingMetrics.set(true);
    this.tradeService.updateMetrics(t.id, this.metricsRR, this.metricsActualRR, this.metricsRiskPercent).subscribe({
      next: () => {
        this.savingMetrics.set(false);
        this.trade.update(tr => tr ? {
          ...tr,
          rr: this.metricsRR,
          actualRR: this.metricsActualRR,
          riskPercent: this.metricsRiskPercent
        } : tr);
        this.messageService.add({ severity: 'success', summary: 'Saved', detail: 'Metrics updated', life: 2000 });
      },
      error: () => this.savingMetrics.set(false)
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

  navigatePrev(): void {
    const id = this.prevId();
    if (id != null) this.router.navigate(['/trades', id]);
  }

  navigateNext(): void {
    const id = this.nextId();
    if (id != null) this.router.navigate(['/trades', id]);
  }
}
