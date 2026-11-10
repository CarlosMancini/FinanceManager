import { Routes } from '@angular/router';
import { AccountsComponent } from './accounts/accounts.component';
import { requireSession } from './auth/auth.service';
import { LoginComponent } from './auth/login.component';

export const routes: Routes = [
  { path: '', component: AccountsComponent, canActivate: [requireSession], pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: '**', redirectTo: '' },
];
