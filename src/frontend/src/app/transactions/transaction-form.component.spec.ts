import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { AccountsApiService } from '../accounts/accounts-api.service';
import { TransactionCommandApiService, TransactionRevision } from './transaction-command-api.service';
import { TransactionFormComponent } from './transaction-form.component';

describe('TransactionFormComponent', () => {
  const activeAccount = { id: 'account-1', name: 'Conta', type: 'checking' as const, currency: 'BRL' as const, status: 'active' as const };
  const revision: TransactionRevision = {
    transaction: {
      id: 'transaction-1', accountId: activeAccount.id, kind: 'expense', amount: '12.34',
      transactionDate: '2026-11-10', description: 'Teste', currency: 'BRL', source: 'manual', status: 'active',
    },
    version: '"1"',
  };

  async function createComponent(id: string | null = null) {
    const create = vi.fn().mockResolvedValue(revision);
    const edit = vi.fn().mockImplementation(async (_id, _version, draft) => ({
      transaction: { ...revision.transaction, ...draft }, version: '"2"',
    }));
    const cancel = vi.fn().mockResolvedValue({
      transaction: { ...revision.transaction, status: 'canceled' }, version: '"2"',
    });
    const navigate = vi.fn().mockResolvedValue(true);
    await TestBed.configureTestingModule({
      imports: [TransactionFormComponent],
      providers: [
        { provide: AccountsApiService, useValue: { list: vi.fn().mockResolvedValue([activeAccount]) } },
        { provide: TransactionCommandApiService, useValue: { create, read: vi.fn().mockResolvedValue(revision), edit, cancel } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: (key: string) => key === 'id' ? id : null } } } },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(TransactionFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    return { component: fixture.componentInstance, create, edit, cancel, navigate };
  }

  it('asks whether another transaction should be added after creation', async () => {
    const { component, create, navigate } = await createComponent();
    component.amountDisplay = 'R$ 12,34';
    component.draft.description = 'Teste';
    await component.save();
    expect(create).toHaveBeenCalledOnce();
    expect(component.askAnother()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not create when any required field is missing or the amount is zero', async () => {
    const { component, create } = await createComponent();
    component.draft.description = '';
    component.amountDisplay = 'R$ 12,34';
    await component.save();
    expect(create).not.toHaveBeenCalled();
    component.draft.description = 'Teste';
    component.amountDisplay = 'R$ 0,00';
    await component.save();
    expect(create).not.toHaveBeenCalled();
    expect(component.error()).toBeTruthy();
  });

  it('resets defaults and creates a fresh idempotency attempt after choosing yes', async () => {
    const { component, create } = await createComponent();
    component.amountDisplay = 'R$ 12,34';
    component.draft.description = 'Primeira';
    await component.save();
    component.addAnother();
    expect(component.draft).toMatchObject({ accountId: activeAccount.id, kind: 'expense', amount: '', description: '' });
    expect(component.amountDisplay).toBe('');
    component.amountDisplay = 'R$ 5,00';
    component.draft.description = 'Segunda';
    await component.save();
    expect(create.mock.calls[0][0]).not.toBe(create.mock.calls[1][0]);
  });

  it.each(['Não', 'fechamento'])('returns to history when the answer is %s', async () => {
    const { component, navigate } = await createComponent();
    component.amountDisplay = 'R$ 12,34';
    component.draft.description = 'Teste';
    await component.save();
    component.handleAnother(false);
    expect(navigate).toHaveBeenCalledWith(['/transactions']);
  });

  it('preserves the form and does not edit before explicit confirmation', async () => {
    const { component, edit } = await createComponent('transaction-1');
    component.onAmountInput('5678');
    component.draft.description = 'Alterada';
    await component.save();
    expect(component.pendingAction()).toBe('edit');
    expect(edit).not.toHaveBeenCalled();
    await component.confirmAction(false);
    expect(component.draft.description).toBe('Alterada');
    expect(edit).not.toHaveBeenCalled();
    await component.save();
    await component.confirmAction(true);
    expect(edit).toHaveBeenCalledOnce();
  });

  it('does not cancel before confirmation and supports refusal and acceptance', async () => {
    const { component, cancel } = await createComponent('transaction-1');
    component.cancelTransaction();
    expect(component.pendingAction()).toBe('cancel');
    expect(cancel).not.toHaveBeenCalled();
    await component.confirmAction(false);
    expect(cancel).not.toHaveBeenCalled();
    component.cancelTransaction();
    await component.confirmAction(true);
    expect(cancel).toHaveBeenCalledOnce();
    expect(component.canceled()).toBe(true);
  });
});
