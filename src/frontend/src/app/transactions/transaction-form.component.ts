import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Account, AccountsApiService } from '../accounts/accounts-api.service';
import { ConfirmationDialogComponent } from '../shared/confirmation-dialog.component';
import { brlInputFromInvariant, formatBrlInput, invariantFromBrlInput } from './brl-currency';
import { TransactionCommandApiService, TransactionDraft, TransactionRevision } from './transaction-command-api.service';

type PendingAction = 'edit' | 'cancel' | null;

@Component({
  selector: 'app-transaction-form',
  imports: [FormsModule, RouterLink, ConfirmationDialogComponent],
  template: `
    <section class="panel">
      <a routerLink="/transactions">← Histórico</a>
      <h2>{{ revision() ? 'Detalhe da movimentação' : 'Nova movimentação' }}</h2>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      @if (loading()) { <p>Carregando…</p> }
      @if (!loading()) {
        @if (revision()?.transaction?.status === 'canceled') { <p><strong>Status: Cancelada</strong></p> }
        <form (ngSubmit)="save()">
          <label>Conta
            <select name="accountId" [(ngModel)]="draft.accountId" required [disabled]="busy() || canceled()">
              @for (account of selectableAccounts(); track account.id) {
                <option [value]="account.id">{{ account.name }}{{ account.status === 'archived' ? ' (Arquivada)' : '' }}</option>
              }
            </select>
          </label>
          <label>Natureza
            <select name="kind" [(ngModel)]="draft.kind" [disabled]="busy() || canceled()">
              <option value="expense">Despesa</option><option value="income">Receita</option>
            </select>
          </label>
          <label>Valor em BRL
            <input name="amount" [ngModel]="amountDisplay" (ngModelChange)="onAmountInput($event)"
              inputmode="numeric" autocomplete="off" placeholder="R$ 0,00" required [disabled]="busy() || canceled()">
          </label>
          <label>Data <input name="transactionDate" type="date" [(ngModel)]="draft.transactionDate" required [disabled]="busy() || canceled()"></label>
          <label>Descrição <input name="description" [(ngModel)]="draft.description" maxlength="200" required [disabled]="busy() || canceled()"></label>
          @if (!canceled()) {
            <div class="actions">
              <button type="submit" [disabled]="busy() || stale()">{{ revision() ? 'Salvar correção' : 'Registrar' }}</button>
              @if (revision()) { <button type="button" (click)="cancelTransaction()" [disabled]="busy() || stale()">Cancelar movimentação</button> }
              @if (stale()) { <button type="button" (click)="reload()" [disabled]="busy()">Recarregar detalhe</button> }
            </div>
          }
        </form>
      }
    </section>

    <app-confirmation-dialog #actionDialog
      [title]="pendingAction() === 'edit' ? 'Confirmar edição' : 'Confirmar cancelamento'"
      [message]="pendingAction() === 'edit' ? 'Deseja salvar as alterações desta movimentação?' : 'Deseja cancelar esta movimentação?'"
      confirmLabel="Sim" cancelLabel="Não" (answered)="confirmAction($event)" />

    <app-confirmation-dialog #anotherDialog title="Movimentação registrada"
      message="Adicionar mais uma movimentação?" confirmLabel="Sim" cancelLabel="Não"
      (answered)="handleAnother($event)" />
  `,
})
export class TransactionFormComponent implements OnInit {
  private readonly api = inject(TransactionCommandApiService);
  private readonly accountsApi = inject(AccountsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  @ViewChild('actionDialog') private readonly actionDialog?: ConfirmationDialogComponent;
  @ViewChild('anotherDialog') private readonly anotherDialog?: ConfirmationDialogComponent;

  readonly accounts = signal<Account[]>([]);
  readonly revision = signal<TransactionRevision | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly askAnother = signal(false);
  readonly pendingAction = signal<PendingAction>(null);
  private pending: { key: string; draft: TransactionDraft } | null = null;
  amountDisplay = '';
  draft: TransactionDraft = this.newDraft();

  canceled = () => this.revision()?.transaction.status === 'canceled';
  selectableAccounts = () => this.accounts().filter(account => account.status === 'active' || account.id === this.revision()?.transaction.accountId);

  async ngOnInit(): Promise<void> {
    try {
      const accounts = await this.accountsApi.list();
      this.accounts.set(accounts);
      const id = this.route.snapshot.paramMap.get('id');
      if (id) {
        await this.load(id);
      } else if (accounts.length === 0) {
        await this.router.navigate(['/accounts'], { queryParams: { onboarding: 'true' } });
        return;
      } else if (!accounts.some(account => account.status === 'active')) {
        await this.router.navigate(['/transactions'], { queryParams: { unavailable: 'true' } });
        return;
      } else {
        this.resetDraft();
      }
    } catch (error) {
      this.showError(error, 'Não foi possível carregar o formulário.');
    } finally {
      this.loading.set(false);
    }
  }

  onAmountInput(value: string): void {
    const formatted = formatBrlInput(value);
    if (formatted === null) {
      this.error.set('O valor informado excede o limite permitido.');
      return;
    }
    this.amountDisplay = formatted;
    this.draft.amount = invariantFromBrlInput(formatted) ?? '';
    if (this.error() === 'O valor informado excede o limite permitido.') this.error.set('');
  }

  async save(): Promise<void> {
    if (this.busy() || this.stale() || this.canceled()) return;
    if (this.revision()) {
      this.pendingAction.set('edit');
      this.actionDialog?.present();
      return;
    }
    await this.createTransaction();
  }

  cancelTransaction(): void {
    if (!this.revision() || this.busy() || this.stale() || this.canceled()) return;
    this.pendingAction.set('cancel');
    this.actionDialog?.present();
  }

  async confirmAction(confirmed: boolean): Promise<void> {
    const action = this.pendingAction();
    this.pendingAction.set(null);
    if (!confirmed) return;
    if (action === 'edit') await this.editTransaction();
    if (action === 'cancel') await this.performCancel();
  }

  handleAnother(answer: boolean): void {
    if (answer) this.addAnother();
    else this.finishCreation();
  }

  addAnother(): void {
    this.askAnother.set(false);
    this.notice.set('Movimentação registrada. Você pode adicionar outra.');
    this.resetDraft();
  }

  finishCreation(): void {
    this.askAnother.set(false);
    void this.router.navigate(['/transactions']);
  }

  reload(): void {
    const id = this.revision()?.transaction.id;
    if (id) void this.load(id);
  }

  private async createTransaction(): Promise<void> {
    const amount = invariantFromBrlInput(this.amountDisplay);
    if (!amount) {
      this.error.set('Informe um valor maior que zero.');
      return;
    }
    this.draft.amount = amount;
    this.begin();
    try {
      this.pending ??= { key: crypto.randomUUID(), draft: { ...this.draft } };
      await this.api.create(this.pending.key, this.pending.draft);
      this.pending = null;
      this.notice.set('Movimentação registrada.');
      this.askAnother.set(true);
      this.anotherDialog?.present();
    } catch (error) {
      this.showError(error, 'Não foi possível salvar a movimentação.');
    } finally {
      this.busy.set(false);
    }
  }

  private async editTransaction(): Promise<void> {
    const current = this.revision();
    const amount = invariantFromBrlInput(this.amountDisplay);
    if (!current || !amount) {
      this.error.set('Informe um valor maior que zero.');
      return;
    }
    this.draft.amount = amount;
    this.begin();
    try {
      const updated = await this.api.edit(current.transaction.id, current.version, { ...this.draft });
      this.setRevision(updated);
      this.notice.set('Movimentação corrigida.');
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 412) this.stale.set(true);
      this.showError(error, 'Não foi possível salvar a movimentação.');
    } finally {
      this.busy.set(false);
    }
  }

  private async performCancel(): Promise<void> {
    const current = this.revision();
    if (!current || this.busy()) return;
    this.begin();
    try {
      this.setRevision(await this.api.cancel(current.transaction.id, current.version));
      this.notice.set('Movimentação cancelada.');
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 412) this.stale.set(true);
      this.showError(error, 'Não foi possível cancelar a movimentação.');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(id: string): Promise<void> {
    this.setRevision(await this.api.read(id));
  }

  private setRevision(value: TransactionRevision): void {
    this.revision.set(value);
    this.draft = {
      accountId: value.transaction.accountId,
      kind: value.transaction.kind,
      amount: value.transaction.amount,
      transactionDate: value.transaction.transactionDate,
      description: value.transaction.description,
    };
    this.amountDisplay = brlInputFromInvariant(value.transaction.amount);
    this.stale.set(false);
  }

  private resetDraft(): void {
    this.pending = null;
    this.draft = this.newDraft(this.accounts().find(account => account.status === 'active')?.id ?? '');
    this.amountDisplay = '';
    this.error.set('');
  }

  private newDraft(accountId = ''): TransactionDraft {
    return { accountId, kind: 'expense', amount: '', transactionDate: this.localDate(), description: '' };
  }

  private begin(): void {
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
  }

  private showError(error: unknown, fallback: string): void {
    this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : fallback);
  }

  private localDate(): string {
    return new Date().toLocaleDateString('en-CA', { timeZone: 'America/Sao_Paulo' });
  }
}
