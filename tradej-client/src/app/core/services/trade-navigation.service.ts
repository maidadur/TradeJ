import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TradeNavigationService {
  private tradeIds: number[] = [];

  setTradeIds(ids: number[]): void {
    this.tradeIds = ids;
  }

  getPrevId(currentId: number): number | null {
    const idx = this.tradeIds.indexOf(currentId);
    if (idx <= 0) return null;
    return this.tradeIds[idx - 1];
  }

  getNextId(currentId: number): number | null {
    const idx = this.tradeIds.indexOf(currentId);
    if (idx < 0 || idx >= this.tradeIds.length - 1) return null;
    return this.tradeIds[idx + 1];
  }

  getPosition(currentId: number): { current: number; total: number } | null {
    const idx = this.tradeIds.indexOf(currentId);
    if (idx < 0 || this.tradeIds.length === 0) return null;
    return { current: idx + 1, total: this.tradeIds.length };
  }
}
