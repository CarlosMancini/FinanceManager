import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { RegistrationFormComponent } from './registration-form.component';
import { RegistrationTransitionService } from './registration-transition.service';
import { RegistrationData, saoPauloToday, validateRegistration } from './registration-validation';

@Component({ selector: 'app-registration-completion', imports: [FormsModule, RouterLink, RegistrationFormComponent], template: `
<section class="panel" aria-labelledby="completion-title">
  <h2 id="completion-title">Completar cadastro existente</h2>
  @if (loading()) { <p role="status">Verificando autorização…</p> }
  <p #errorSummary role="alert" tabindex="-1" [hidden]="!message()">{{ message() }}</p>
  @if (!loading() && !authorized()) {
    <p>Comprove seu acesso anterior para continuar.</p>
    <form (ngSubmit)="prove()">
      <label>Nome de acesso anterior <input name="legacyUsername" [(ngModel)]="username" autocomplete="username" required></label>
      <label>Senha anterior <input name="legacyPassword" type="password" [(ngModel)]="legacyPassword" autocomplete="current-password" required></label>
      <button type="submit" [disabled]="busy() || !username || !legacyPassword">Comprovar acesso</button>
    </form>
  }
  @if (authorized()) {
    <p role="status">A autorização é válida por dez minutos após a comprovação.</p>
    <app-registration-form [data]="data" [errors]="errors()" [busy]="busy()" (submitted)="submit()" />
  }
  <p><a routerLink="/login">Voltar ao login</a></p>
</section>` })
export class RegistrationCompletionComponent implements OnInit, OnDestroy {
  @ViewChild('errorSummary') private errorSummary?: ElementRef<HTMLElement>;
  private readonly transition = inject(RegistrationTransitionService);
  private readonly router = inject(Router);
  readonly loading = signal(true); readonly busy = signal(false); readonly authorized = signal(false);
  readonly message = signal(''); readonly errors = signal<Record<string, string>>({});
  username = ''; legacyPassword = '';
  data: RegistrationData = { fullName: '', nickname: '', birthDate: '', email: '', phoneNumber: '', password: '', confirmPassword: '' };

  async ngOnInit() {
    try {
      await this.transition.authorization(); this.authorized.set(true);
    } catch { this.authorized.set(false); }
    finally { this.loading.set(false); }
  }

  ngOnDestroy() { this.legacyPassword = ''; this.clearPasswords(); }

  async prove() {
    if (this.busy()) return;
    this.busy.set(true); this.message.set('');
    try { await this.transition.prove(this.username, this.legacyPassword); this.authorized.set(true); }
    catch (error) { this.message.set(this.messageFor(error, 'Não foi possível comprovar o acesso.')); this.focusError(); }
    finally { this.legacyPassword = ''; this.busy.set(false); }
  }

  async submit() {
    if (this.busy()) return;
    const errors = validateRegistration(this.data, saoPauloToday()); this.errors.set(errors);
    if (Object.keys(errors).length) { this.message.set('Confira os campos informados.'); this.focusError(); return; }
    this.busy.set(true); this.message.set('');
    try {
      await this.transition.complete(this.data);
      this.clearPasswords();
      await this.router.navigate(['/login'], { queryParams: { registered: true } });
    } catch (error) {
      const response = error instanceof HttpErrorResponse ? error.error : null;
      if (response?.errors && typeof response.errors === 'object') this.errors.set(Object.fromEntries(Object.entries(response.errors).map(([key, value]) => [key, Array.isArray(value) ? String(value[0]) : 'Confira o valor informado.'])));
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.authorized.set(false);
        this.clearPasswords();
        this.message.set('A autorização não está mais válida. Comprove o acesso novamente.');
      } else {
        this.message.set(this.messageFor(error, 'Não foi possível concluir o cadastro. Tente novamente.'));
      }
      if (response?.code === 'EMAIL_ALREADY_REGISTERED') this.errors.set({ email: 'Este e-mail já está registrado. Entre ou informe outro e-mail.' });
      this.focusError();
    } finally { this.busy.set(false); }
  }
  private clearPasswords() { this.data.password = ''; this.data.confirmPassword = ''; }

  private messageFor(error: unknown, fallback: string) {
    return error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : fallback;
  }
  private focusError() { setTimeout(() => this.errorSummary?.nativeElement.focus()); }
}
