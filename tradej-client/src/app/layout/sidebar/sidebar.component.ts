import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { Account } from '../../core/models/account.model';
import { AccountService } from '../../core/services/account.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, CommonModule, SelectModule, FormsModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit {
  accountService = inject(AccountService);
  accounts = signal<Account[]>([]);
  selectedAccount: Account | null = null;

  ngOnInit(): void {
    this.accountService.getAll().subscribe(list => {
      this.accounts.set(list);
    });
    this.accountService.selectedAccount$.subscribe(a => {
      this.selectedAccount = a;
    });
  }

  onAccountChange(account: Account): void {
    this.accountService.selectAccount(account);
  }
}
