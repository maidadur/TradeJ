import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { AccountService } from '../../core/services/account.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent],
  templateUrl: './app-layout.component.html'
})
export class AppLayoutComponent implements OnInit {
  private accountService = inject(AccountService);

  ngOnInit(): void {
    // Bootstrap accounts on app start
    this.accountService.getAll().subscribe();
  }
}
