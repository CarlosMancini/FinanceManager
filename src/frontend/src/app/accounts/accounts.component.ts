import { HttpErrorResponse } from '@angular/common/http';
import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Account, AccountDraft, AccountRevision, AccountsApiService } from './accounts-api.service';

@Component({
  selector: 'app-accounts',
  imports: [FormsModule],
  template: `
    <section class="panel" aria-labelledby="accounts-title">
      <h2 id="accounts-title">Contas financeiras</h2>
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (loading()) {
        <p role="status">Carregando contas…</p>
      } @else if (loadFailed()) {
        <button type="button" (click)="load()" [disabled]="busy()">Recarregar lista</button>
      } @else {
        @if (accounts().length === 0) { <p>Nenhuma conta cadastrada.</p> }
        <ul class="accounts">
          @for (account of accounts(); track account.id) {
            <li>
              <div>
                <strong>{{ account.name }}</strong>
                <div>{{ types[account.type] }} · {{ account.currency }} · {{ account.status === 'active' ? 'Ativa' : 'Arquivada' }}</div>
              </div>
              <div class="actions">
                <button type="button" (click)="open(account.id)" [disabled]="busy()" [attr.aria-label]="'Editar ' + account.name">Editar</button>
                <button type="button" (click)="toggle(account)" [disabled]="busy()"
                  [attr.aria-label]="(account.status === 'active' ? 'Arquivar ' : 'Reativar ') + account.name">
                  {{ account.status === 'active' ? 'Arquivar' : 'Reativar' }}
                </button>
              </div>
            </li>
          }
        </ul>
      }
    </section>

    <section class="panel" aria-labelledby="editor-title">
      <h2 id="editor-title">{{ selected() ? 'Editar conta' : 'Cadastrar conta' }}</h2>
      <form (ngSubmit)="save()">
        <label>Nome <input #nameInput name="name" [(ngModel)]="draft.name" required [disabled]="busy()"></label>
        <label>Tipo
          <select name="type" [(ngModel)]="draft.type" [disabled]="busy()">
            <option value="checking">Corrente</option>
            <option value="savings">Poupança</option>
            <option value="wallet">Carteira</option>
          </select>
        </label>
        <div class="actions">
          <button type="submit" [disabled]="busy() || stale()">{{ selected() ? 'Salvar' : 'Cadastrar' }}</button>
          @if (stale()) {
            <button type="button" (click)="reloadSelected()" [disabled]="busy()">Recarregar conta</button>
          }
          @if (selected()) {
            <button type="button" (click)="cancel()" [disabled]="busy()">Cancelar edição</button>
          }
        </div>
      </form>
    </section>
  `,
})
export class AccountsComponent implements OnInit, AfterViewInit {
  private readonly api = inject(AccountsApiService);
  private readonly route = inject(ActivatedRoute);
  @ViewChild('nameInput', { static: true }) private readonly nameInput?: ElementRef<HTMLInputElement>;
  readonly accounts = signal<Account[]>([]);
  readonly selected = signal<AccountRevision | null>(null);
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly types = { checking: 'Corrente', savings: 'Poupança', wallet: 'Carteira' };
  draft: AccountDraft = { name: '', type: 'checking' };

  ngOnInit(): void {
    void this.load();
  }

  ngAfterViewInit(): void {
    if (this.route.snapshot.queryParamMap.get('onboarding') === 'true') this.nameInput?.nativeElement.focus();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.error.set('');
    try {
      this.accounts.set(await this.api.list());
    } catch (error) {
      this.loadFailed.set(true);
      this.showError(error, 'Não foi possível carregar as contas.');
    } finally {
      this.loading.set(false);
    }
  }

  async open(id: string): Promise<void> {
    if (this.busy()) return;
    this.begin();
    try {
      const revision = await this.api.read(id);
      this.selected.set(revision);
      this.draft = { name: revision.account.name, type: revision.account.type };
      this.stale.set(false);
    } catch (error) {
      this.showError(error, 'Não foi possível carregar a conta.');
    } finally {
      this.busy.set(false);
    }
  }

  reloadSelected(): void {
    const selected = this.selected();
    if (selected) void this.open(selected.account.id);
  }

  cancel(): void {
    this.selected.set(null);
    this.stale.set(false);
    this.draft = { name: '', type: 'checking' };
    this.error.set('');
  }

  async save(): Promise<void> {
    if (this.busy() || this.stale()) return;
    const current = this.selected();
    this.begin();
    try {
      if (current) {
        await this.api.edit(current.account.id, current.version, this.draft);
      } else {
        await this.api.create(this.draft);
      }
      this.cancel();
      this.notice.set(current ? 'Alterações salvas.' : 'Conta cadastrada.');
      await this.load();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 412) this.stale.set(true);
      this.showError(error, 'Não foi possível salvar a conta.');
    } finally {
      this.busy.set(false);
    }
  }

  async toggle(account: Account): Promise<void> {
    if (this.busy()) return;
    this.begin();
    try {
      const current = await this.api.read(account.id);
      const target = account.status === 'active' ? 'archived' : 'active';
      if (current.account.status !== account.status) {
        await this.load();
        this.error.set('A conta mudou. Revise o estado atualizado antes de tentar novamente.');
        return;
      }
      await this.api.changeStatus(account.id, current.version, target);
      this.notice.set(target === 'archived' ? 'Conta arquivada.' : 'Conta reativada.');
      await this.load();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 412) await this.load();
      this.showError(error, 'Não foi possível alterar o estado da conta.');
    } finally {
      this.busy.set(false);
    }
  }

  private begin(): void {
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
  }

  private showError(error: unknown, fallback: string): void {
    this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
      ? error.error.title : fallback);
  }
}
