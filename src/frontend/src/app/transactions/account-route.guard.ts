import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Account, AccountsApiService } from '../accounts/accounts-api.service';

export type AccountRouteTarget = 'history' | 'new';
export type AccountRouteDecision = 'allow' | 'accounts' | 'history';

export function hasActiveAccount(accounts: Account[]): boolean {
  return accounts.some(account => account.status === 'active');
}

export function accountRouteDecision(accounts: Account[], target: AccountRouteTarget): AccountRouteDecision {
  if (accounts.length === 0) return 'accounts';
  if (target === 'new' && !hasActiveAccount(accounts)) return 'history';
  return 'allow';
}

function accountGuard(target: AccountRouteTarget): CanActivateFn {
  return async () => {
    const accountsApi = inject(AccountsApiService);
    const router = inject(Router);
    const accounts = await accountsApi.list();
    const decision = accountRouteDecision(accounts, target);
    if (decision === 'accounts') return router.createUrlTree(['/accounts'], { queryParams: { onboarding: 'true' } });
    if (decision === 'history') return router.createUrlTree(['/transactions'], { queryParams: { unavailable: 'true' } });
    return true;
  };
}

export const requireAnyAccount = accountGuard('history');
export const requireActiveAccount = accountGuard('new');
