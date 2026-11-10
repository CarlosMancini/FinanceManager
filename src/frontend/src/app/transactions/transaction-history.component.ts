import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Account, AccountsApiService } from '../accounts/accounts-api.service';
import { hasActiveAccount } from './account-route.guard';
import { TransactionHistory, TransactionHistoryApiService } from './transaction-history-api.service';

@Component({
  selector: 'app-transaction-history', imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <div class="actions">
        <h2>Movimentações</h2>
        @if (hasActiveAccounts()) {
          <a class="button-link" routerLink="/transactions/new">Nova movimentação</a>
        } @else {
          <button type="button" disabled title="Reative ou cadastre uma conta para criar movimentações.">Nova movimentação</button>
        }
      </div>
      <form (ngSubmit)="apply()" class="filters">
        <label>Conta <select name="account" [(ngModel)]="accountId"><option value="">Todas</option>@for (account of accounts(); track account.id) { <option [value]="account.id">{{ account.name }}{{ account.status === 'archived' ? ' (Arquivada)' : '' }}</option> }</select></label>
        <label>Natureza <select name="kind" [(ngModel)]="kind"><option value="">Todas</option><option value="income">Receitas</option><option value="expense">Despesas</option></select></label>
        <label>De <input name="from" type="date" [(ngModel)]="from"></label>
        <label>Até <input name="to" type="date" [(ngModel)]="to"></label>
        <button type="submit" [disabled]="loading()">Aplicar filtros</button>
      </form>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p role="status">Carregando histórico…</p> }
      @if (!loading() && result(); as history) {
        <div class="totals"><span>Receitas <strong>{{ money(history.totals.income) }}</strong></span><span>Despesas <strong>{{ money(history.totals.expense) }}</strong></span><span>Líquido <strong>{{ money(history.totals.net) }}</strong></span></div>
        @if (history.items.length === 0) { <p>Nenhuma movimentação encontrada.</p> }
        <ul class="transactions">@for (item of history.items; track item.id) { <li><div><strong>{{ item.description }}</strong><div>{{ item.transactionDate }} · {{ item.kind === 'income' ? 'Receita' : 'Despesa' }} · {{ money(item.amount) }} @if (item.status === 'canceled') { · <strong>Cancelada</strong> }</div></div><a [routerLink]="['/transactions', item.id]">Detalhe</a></li> }</ul>
        <div class="actions"><button type="button" (click)="page(history.page - 1)" [disabled]="history.page <= 1 || loading()">Anterior</button><span>Página {{ history.page }}</span><button type="button" (click)="page(history.page + 1)" [disabled]="history.page * history.pageSize >= history.totalCount || loading()">Próxima</button></div>
      }
    </section>
  `,
})
export class TransactionHistoryComponent implements OnInit {
  private readonly api = inject(TransactionHistoryApiService); private readonly accountsApi = inject(AccountsApiService);
  readonly accounts = signal<Account[]>([]); readonly result = signal<TransactionHistory | null>(null); readonly loading = signal(false); readonly error = signal('');
  accountId = ''; kind: '' | 'income' | 'expense' = ''; from = ''; to = '';
  hasActiveAccounts = () => hasActiveAccount(this.accounts());
  async ngOnInit(): Promise<void> { try { this.accounts.set(await this.accountsApi.list()); await this.load(1, false); } catch (error) { this.showError(error); } }
  apply(): void { void this.load(1, true); } page(value: number): void { void this.load(value, true); }
  money(value: string): string { const negative = value.startsWith('-'); const [whole, cents = '00'] = (negative ? value.slice(1) : value).split('.'); const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.'); return `${negative ? '-' : ''}R$ ${grouped},${cents.padEnd(2, '0')}`; }
  private async load(page: number, explicit: boolean): Promise<void> { this.loading.set(true); this.error.set(''); try { const value = await this.api.query({ accountId: this.accountId || undefined, kind: this.kind || undefined, from: explicit && this.from ? this.from : undefined, to: explicit && this.to ? this.to : undefined, page, pageSize: 25 }); this.result.set(value); this.from = value.from; this.to = value.to; } catch (error) { this.showError(error); } finally { this.loading.set(false); } }
  private showError(error: unknown): void { this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : 'Não foi possível carregar o histórico.'); }
}
