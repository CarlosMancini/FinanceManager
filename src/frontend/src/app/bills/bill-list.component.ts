import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatBrlInput, invariantFromBrlInput } from '../transactions/brl-currency';
import { BillApiService, BillDraft, BillFilterStatus, BillPage } from './bill-api.service';

@Component({
  selector: 'app-bill-list',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <div class="actions"><h2>Contas a pagar</h2><a class="button-link" href="#new-bill">Nova obrigação</a></div>
      <form class="filters" (ngSubmit)="applyFilters()">
        <label>Situação <select name="status" [(ngModel)]="status"><option value="open">Em aberto</option><option value="upcoming">A vencer</option><option value="dueToday">Vence hoje</option><option value="overdue">Vencidas</option><option value="paid">Pagas</option><option value="canceled">Canceladas</option><option value="all">Todas</option></select></label>
        <label>De <input name="from" type="date" [(ngModel)]="from"></label>
        <label>Até <input name="to" type="date" [(ngModel)]="to"></label>
        <button type="submit" [disabled]="loading()">Aplicar filtros</button>
      </form>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) { <p role="status">Carregando obrigações…</p> }
      @else if (result(); as page) {
        <div class="totals"><span>Total em aberto <strong>R$ {{ page.totals.openAmount }}</strong></span><span>Resultados <strong>{{ page.totalCount }}</strong></span></div>
        @if (page.items.length === 0) { <p>Nenhuma obrigação encontrada.</p> }
        <ul class="transactions">
          @for (bill of page.items; track bill.id) { <li><div><strong>{{ bill.description }}</strong><div>{{ labels[bill.status] }} · vencimento {{ bill.dueDate }}</div></div><div><strong>R$ {{ bill.amount }}</strong><br><a [routerLink]="['/bills', bill.id]">Abrir</a></div></li> }
        </ul>
        <div class="actions"><button type="button" (click)="changePage(page.page - 1)" [disabled]="page.page <= 1 || loading()">Anterior</button><span>Página {{ page.page }}</span><button type="button" (click)="changePage(page.page + 1)" [disabled]="page.page * page.pageSize >= page.totalCount || loading()">Próxima</button></div>
      }
    </section>
    <section class="panel" id="new-bill"><h2>Nova obrigação</h2>
      <form (ngSubmit)="create()"><label>Descrição <input name="description" [(ngModel)]="draft.description" required></label><label>Valor em BRL <input name="amount" [ngModel]="amountDisplay" (ngModelChange)="onAmountInput($event)" inputmode="numeric" autocomplete="off" placeholder="R$ 0,00" required></label><label>Vencimento <input name="dueDate" type="date" [(ngModel)]="draft.dueDate" required></label><button type="submit" [disabled]="saving()">{{ saving() ? 'Salvando…' : 'Cadastrar' }}</button></form>
    </section>
  `,
})
export class BillListComponent implements OnInit {
  private readonly api = inject(BillApiService);
  readonly result = signal<BillPage | null>(null);
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal('');
  readonly labels = { upcoming: 'A vencer', dueToday: 'Vence hoje', overdue: 'Vencida', paid: 'Paga', canceled: 'Cancelada' };
  status: BillFilterStatus = 'open'; from = ''; to = ''; draft: BillDraft = { description: '', amount: '', dueDate: '' };
  amountDisplay = '';
  ngOnInit(): void { void this.load(1); }
  applyFilters(): void { void this.load(1); }
  changePage(page: number): void { void this.load(page); }
  async load(page: number): Promise<void> { this.loading.set(true); this.error.set(''); try { this.result.set(await this.api.list({ status: this.status, from: this.from || undefined, to: this.to || undefined, page, pageSize: 25 })); } catch (error) { this.showError(error, 'Não foi possível carregar as obrigações.'); } finally { this.loading.set(false); } }
  onAmountInput(value: string): void { const formatted = formatBrlInput(value); if (formatted === null) { this.error.set('O valor informado excede o limite permitido.'); return; } this.amountDisplay = formatted; this.draft.amount = invariantFromBrlInput(formatted) ?? ''; }
  async create(): Promise<void> { if (this.saving()) return; const amount = invariantFromBrlInput(this.amountDisplay); if (!this.draft.description.trim() || !this.draft.dueDate || !amount) { this.error.set('Preencha todos os campos obrigatórios e informe um valor maior que zero.'); return; } this.draft.amount = amount; this.saving.set(true); this.error.set(''); try { await this.api.create(this.draft); this.draft = { description: '', amount: '', dueDate: '' }; this.amountDisplay = ''; await this.load(1); } catch (error) { this.showError(error, 'Não foi possível cadastrar a obrigação.'); } finally { this.saving.set(false); } }
  private showError(error: unknown, fallback: string): void { this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : fallback); }
}
