import { Component, OnInit, HostListener, ElementRef, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Account } from '../../core/models/account.model';
import { AccountService } from '../../core/services/account.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit {
  private accountService = inject(AccountService);
  private elRef        = inject(ElementRef);

  accounts     = signal<Account[]>([]);
  selectedIds  = signal<number[]>([]);
  panelOpen    = false;

  ngOnInit(): void {
    this.accountService.getAll().subscribe(list => this.accounts.set(list));
    this.accountService.selectedAccountIds$.subscribe(ids => this.selectedIds.set(ids));
  }

  get allSelected(): boolean {
    return this.accounts().length > 0 &&
           this.selectedIds().length === this.accounts().length;
  }

  get someSelected(): boolean {
    return this.selectedIds().length > 0 && !this.allSelected;
  }

  get triggerLabel(): string {
    const ids = this.selectedIds();
    const all = this.accounts();
    if (!all.length)      return 'No accounts';
    if (!ids.length)      return 'No account selected';
    if (ids.length === all.length) return 'All accounts';
    if (ids.length === 1) return all.find(a => a.id === ids[0])?.name ?? '1 account';
    return `${ids.length} accounts`;
  }

  togglePanel(): void { this.panelOpen = !this.panelOpen; }

  toggleAll(): void {
    this.accountService.selectAccountIds(
      this.allSelected ? [] : this.accounts().map(a => a.id)
    );
  }

  toggleAccount(id: number): void {
    const cur = this.selectedIds();
    this.accountService.selectAccountIds(
      cur.includes(id) ? cur.filter(i => i !== id) : [...cur, id]
    );
  }

  isSelected(id: number): boolean { return this.selectedIds().includes(id); }

  @HostListener('document:click', ['$event'])
  onDocumentClick(e: MouseEvent): void {
    if (!this.elRef.nativeElement.contains(e.target as Node)) {
      this.panelOpen = false;
    }
  }
}

