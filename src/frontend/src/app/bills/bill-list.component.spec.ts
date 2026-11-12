import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { BillApiService } from './bill-api.service';
import { BillListComponent } from './bill-list.component';

describe('BillListComponent', () => {
  it('masks digits and blocks missing fields and zero before creating', async () => {
    const create = vi.fn().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({
      imports: [BillListComponent],
      providers: [{ provide: BillApiService, useValue: { list: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 25, totalCount: 0, totals: { openAmount: '0.00' } }), create } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(BillListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.onAmountInput('R$ -1a2.3,4x');
    expect(component.amountDisplay).toBe('R$ 12,34');
    component.draft.description = '';
    component.draft.dueDate = '2026-11-11';
    await component.create();
    expect(create).not.toHaveBeenCalled();
    component.draft.description = 'Conta';
    component.onAmountInput('0');
    await component.create();
    expect(create).not.toHaveBeenCalled();
    component.onAmountInput('1');
    await component.create();
    expect(create).toHaveBeenCalledWith({ description: 'Conta', amount: '0.01', dueDate: '2026-11-11' });
  });
});
