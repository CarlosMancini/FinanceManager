import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BillApiService, BillDraft, BillRevision } from './bill-api.service';
import { BillSettlementComponent } from './bill-settlement.component';
import { BillSettlementApiService } from './bill-settlement-api.service';

@Component({ selector: 'app-bill-detail', imports: [FormsModule, RouterLink, BillSettlementComponent], template: `
  <section class="panel"><p><a routerLink="/bills">← Voltar</a></p><h2>Detalhe da obrigação</h2>
  @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
  @if (loading()) { <p role="status">Carregando obrigação…</p> }
  @else if (current(); as row) { <p><strong>{{ row.bill.description }}</strong> · R$ {{ row.bill.amount }} · {{ row.bill.dueDate }}</p><p>Situação: {{ row.bill.status }}</p>
    @if (row.bill.status !== 'paid' && row.bill.status !== 'canceled') { <form (ngSubmit)="save()"><label>Descrição <input name="description" [(ngModel)]="draft.description"></label><label>Valor <input name="amount" [(ngModel)]="draft.amount"></label><label>Vencimento <input name="dueDate" type="date" [(ngModel)]="draft.dueDate"></label><div class="actions"><button type="submit" [disabled]="busy() || stale()">Salvar</button><button type="button" (click)="cancel()" [disabled]="busy() || stale()">Cancelar obrigação</button>@if (stale()) { <button type="button" (click)="load()">Recarregar</button> }</div></form><app-bill-settlement [revision]="row" (completed)="load()" /> }
    @if (row.bill.activeSettlement; as settlement) { <p>A baixa {{ settlement.origin === 'createdExpense' ? 'criou uma despesa, que será cancelada na reversão.' : 'vinculou uma despesa existente, que permanecerá ativa.' }}</p><button type="button" (click)="reverse(settlement.id)" [disabled]="busy()">Reverter baixa</button> }
  }
  </section>` })
export class BillDetailComponent implements OnInit {
  private readonly api = inject(BillApiService); private readonly settlements = inject(BillSettlementApiService); private readonly route = inject(ActivatedRoute);
  readonly current = signal<BillRevision | null>(null); readonly loading = signal(false); readonly busy = signal(false); readonly stale = signal(false); readonly error = signal('');
  draft: BillDraft = { description: '', amount: '', dueDate: '' };
  ngOnInit(): void { void this.load(); }
  async load(): Promise<void> { this.loading.set(true); this.error.set(''); try { const row = await this.api.read(this.route.snapshot.paramMap.get('id')!); this.current.set(row); this.draft = { description: row.bill.description, amount: row.bill.amount, dueDate: row.bill.dueDate }; this.stale.set(false); } catch (error) { this.show(error, 'Não foi possível carregar a obrigação.'); } finally { this.loading.set(false); } }
  async save(): Promise<void> { const row = this.current(); if (!row || this.busy() || this.stale()) return; this.busy.set(true); try { this.current.set(await this.api.edit(row.bill.id, row.version, this.draft)); } catch (error) { this.handleMutation(error); } finally { this.busy.set(false); } }
  async cancel(): Promise<void> { const row = this.current(); if (!row || this.busy() || this.stale() || !confirm('Cancelar esta obrigação?')) return; this.busy.set(true); try { this.current.set(await this.api.cancel(row.bill.id, row.version)); } catch (error) { this.handleMutation(error); } finally { this.busy.set(false); } }
  async reverse(id: string): Promise<void> { const row = this.current(); if (!row || this.busy() || !confirm(row.bill.activeSettlement?.origin === 'createdExpense' ? 'A despesa criada será cancelada. Reverter?' : 'A despesa vinculada permanecerá ativa. Reverter?')) return; this.busy.set(true); try { await this.settlements.reverse(row.bill.id, id); await this.load(); } catch (error) { this.handleMutation(error); } finally { this.busy.set(false); } }
  private handleMutation(error: unknown): void { if (error instanceof HttpErrorResponse && error.status === 412) this.stale.set(true); this.show(error, 'Não foi possível alterar a obrigação.'); }
  private show(error: unknown, fallback: string): void { this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : fallback); }
}
