import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { StrategyService } from '../../../core/services/strategy.service';
import { StrategyListItem, CreateStrategyDto } from '../../../core/models/strategy.model';
import { AccountService } from '../../../core/services/account.service';

@Component({
  selector: 'app-strategy-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    ButtonModule, DialogModule, InputTextModule, TextareaModule
  ],
  templateUrl: './strategy-list.component.html',
  styleUrl: './strategy-list.component.scss'
})
export class StrategyListComponent implements OnInit {
  private strategyService = inject(StrategyService);
  private accountService = inject(AccountService);

  strategies = signal<StrategyListItem[]>([]);
  loading = signal(false);

  showCreateDialog = false;
  newName = '';
  newDescription = '';
  creating = false;

  get accountId(): number {
    return this.accountService.selectedAccount?.id ?? 0;
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (!this.accountId) return;
    this.loading.set(true);
    this.strategyService.getAll(this.accountId).subscribe({
      next: items => { this.strategies.set(items); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  openCreateDialog(): void {
    this.newName = '';
    this.newDescription = '';
    this.showCreateDialog = true;
  }

  createStrategy(): void {
    if (!this.newName.trim()) return;
    this.creating = true;
    const dto: CreateStrategyDto = {
      name: this.newName.trim(),
      description: this.newDescription.trim() || undefined
    };
    this.strategyService.create(this.accountId, dto).subscribe({
      next: detail => {
        this.strategies.update(list => [...list, {
          id: detail.id,
          name: detail.name,
          description: detail.description,
          imageUrl: detail.imageUrl,
          tradeCount: 0,
          winRate: 0,
          netPnL: 0
        }]);
        this.showCreateDialog = false;
        this.creating = false;
      },
      error: () => { this.creating = false; }
    });
  }

  formatPnL(v: number): string {
    return (v >= 0 ? '+' : '') + v.toFixed(2);
  }
}
