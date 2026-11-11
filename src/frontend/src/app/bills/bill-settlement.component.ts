import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Account, AccountsApiService } from '../accounts/accounts-api.service';
import { BillRevision } from './bill-api.service';
import { BillSettlementApiService, EligibleExpense, SettlementBody } from './bill-settlement-api.service';

@Component({ selector: 'app-bill-settlement', imports: [FormsModule], template: `
  <section aria-labelledby="settlement-title"><h3 id="settlement-title">Dar baixa integral</h3>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    <p>Valor e descrição: <strong>R$ {{ revision.bill.amount }} · {{ revision.bill.description }}</strong></p>
    <form (ngSubmit)="submit()"><label>Forma <select name="mode" [(ngModel)]="mode" (ngModelChange)="newAttempt()"><option value="link">Vincular despesa existente</option><option value="create">Criar despesa</option></select></label>
      @if (mode === 'link') { <label>Despesa <select name="expense" [(ngModel)]="transactionId" required (ngModelChange)="newAttempt()"><option value="">Selecione</option>@for (item of eligible(); track item.transactionId) { <option [value]="item.transactionId">{{ item.transactionDate }} · {{ item.description }}{{ item.accountArchived ? ' · conta arquivada' : '' }}</option> }</select></label>@if (!loading() && eligible().length === 0) { <p>Nenhuma despesa elegível.</p> } }
      @else { <label>Conta ativa <select name="account" [(ngModel)]="accountId" required (ngModelChange)="newAttempt()"><option value="">Selecione</option>@for (account of activeAccounts(); track account.id) { <option [value]="account.id">{{ account.name }}</option> }</select></label><label>Data do pagamento <input name="paymentDate" type="date" [(ngModel)]="paymentDate" required (ngModelChange)="newAttempt()"></label> }
      <button type="submit" [disabled]="busy() || loading()">{{ busy() ? 'Processando…' : 'Confirmar baixa' }}</button>
    </form>
  </section>` })
export class BillSettlementComponent implements OnInit {
  @Input({ required: true }) revision!: BillRevision;
  @Output() readonly completed = new EventEmitter<void>();
  private readonly api = inject(BillSettlementApiService); private readonly accountsApi = inject(AccountsApiService);
  readonly eligible = signal<EligibleExpense[]>([]); readonly accounts = signal<Account[]>([]); readonly loading = signal(false); readonly busy = signal(false); readonly error = signal('');
  mode: 'link' | 'create' = 'link'; transactionId = ''; accountId = ''; paymentDate = new Date().toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
  private attempt: { key: string; body: SettlementBody } | null = null;
  activeAccounts = () => this.accounts().filter(account => account.status === 'active');
  async ngOnInit(): Promise<void> { this.loading.set(true); try { const [eligible, accounts] = await Promise.all([this.api.eligible(this.revision.bill.id), this.accountsApi.list()]); this.eligible.set(eligible.items); this.accounts.set(accounts); } catch (error) { this.show(error); } finally { this.loading.set(false); } }
  newAttempt(): void { this.attempt = null; }
  async submit(): Promise<void> { if (this.busy()) return; if (!this.attempt) { const body = this.body(); if (!body) { this.error.set('Preencha os dados da baixa.'); return; } this.attempt = { key: crypto.randomUUID(), body }; } this.busy.set(true); this.error.set(''); try { await this.api.settle(this.revision.bill.id, this.attempt.key, this.attempt.body); this.completed.emit(); } catch (error) { this.show(error); } finally { this.busy.set(false); } }
  private body(): SettlementBody | null { if (this.mode === 'link') { const item = this.eligible().find(value => value.transactionId === this.transactionId); return item ? { mode: 'link', billVersion: this.revision.version, transactionId: item.transactionId, transactionVersion: item.transactionVersion, accountId: null, paymentDate: null } : null; } return this.accountId && this.paymentDate ? { mode: 'create', billVersion: this.revision.version, transactionId: null, transactionVersion: null, accountId: this.accountId, paymentDate: this.paymentDate } : null; }
  private show(error: unknown): void { this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : 'Não foi possível concluir a baixa. Você pode reenviar a mesma tentativa.'); }
}
