import { describe, expect, it } from 'vitest';
import { Account } from '../accounts/accounts-api.service';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AccountsApiService } from '../accounts/accounts-api.service';
import { accountRouteDecision, hasActiveAccount, requireAnyAccount } from './account-route.guard';

const account = (status: Account['status']): Account => ({
  id: status,
  name: status,
  type: 'checking',
  currency: 'BRL',
  status,
});

describe('account route decision', () => {
  it('sends an account-less user to account creation from the landing page', () => {
    expect(accountRouteDecision([], 'history')).toBe('accounts');
  });

  it('allows history when at least one account exists', () => {
    expect(accountRouteDecision([account('archived')], 'history')).toBe('allow');
  });

  it('allows new transactions only when an active account exists', () => {
    expect(accountRouteDecision([account('active')], 'new')).toBe('allow');
    expect(accountRouteDecision([account('archived')], 'new')).toBe('history');
    expect(accountRouteDecision([], 'new')).toBe('accounts');
  });

  it('disables the new transaction action without an active account', () => {
    expect(hasActiveAccount([])).toBe(false);
    expect(hasActiveAccount([account('archived')])).toBe(false);
    expect(hasActiveAccount([account('active')])).toBe(true);
  });

  it('keeps all injected dependencies available across the asynchronous account lookup', async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: AccountsApiService, useValue: { list: async () => [] } },
        { provide: Router, useValue: { createUrlTree: () => 'accounts-url-tree' } },
      ],
    }).compileComponents();

    const result = await TestBed.runInInjectionContext(() => requireAnyAccount({} as never, {} as never));
    expect(result).toBe('accounts-url-tree');
  });
});
