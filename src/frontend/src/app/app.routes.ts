import { Routes } from '@angular/router';
import { AccountsComponent } from './accounts/accounts.component';
import { requireSession } from './auth/auth.service';
import { LoginComponent } from './auth/login.component';
import { TransactionFormComponent } from './transactions/transaction-form.component';
import { TransactionHistoryComponent } from './transactions/transaction-history.component';
import { requireActiveAccount, requireAnyAccount } from './transactions/account-route.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'transactions', pathMatch: 'full' },
  { path: 'accounts', component: AccountsComponent, canActivate: [requireSession] },
  { path: 'transactions', component: TransactionHistoryComponent, canActivate: [requireSession, requireAnyAccount] },
  { path: 'transactions/new', component: TransactionFormComponent, canActivate: [requireSession, requireActiveAccount] },
  { path: 'transactions/:id', component: TransactionFormComponent, canActivate: [requireSession] },
  { path: 'login', component: LoginComponent },
  { path: '**', redirectTo: 'transactions' },
];
