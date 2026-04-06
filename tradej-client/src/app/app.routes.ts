import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-layout/app-layout.component').then(m => m.AppLayoutComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'trades',
        loadComponent: () => import('./features/trades/trade-list/trade-list.component').then(m => m.TradeListComponent)
      },
      {
        path: 'day-view',
        loadComponent: () => import('./features/day-view/day-view.component').then(m => m.DayViewComponent)
      },
      {
        path: 'trades/:id',
        loadComponent: () => import('./features/trades/trade-detail/trade-detail.component').then(m => m.TradeDetailComponent)
      },
      {
        path: 'import',
        loadComponent: () => import('./features/import/import.component').then(m => m.ImportComponent)
      },
      {
        path: 'notebook',
        loadComponent: () => import('./features/notebook/notebook.component').then(m => m.NotebookComponent)
      },
      {
        path: 'strategies',
        loadComponent: () => import('./features/strategies/strategy-list/strategy-list.component').then(m => m.StrategyListComponent)
      },
      {
        path: 'strategies/:id',
        loadComponent: () => import('./features/strategies/strategy-detail/strategy-detail.component').then(m => m.StrategyDetailComponent)
      },
      {
        path: 'accounts',
        loadComponent: () => import('./features/accounts/accounts.component').then(m => m.AccountsComponent)
      }
    ]
  },
  {
    path: 'ctrader-callback',
    loadComponent: () => import('./features/ctrader-callback/ctrader-callback.component')
      .then(m => m.CTraderCallbackComponent)
  },
  { path: '**', redirectTo: 'dashboard' }
];
