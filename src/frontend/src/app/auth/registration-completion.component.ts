import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { RegistrationFormComponent } from './registration-form.component';
import { RegistrationService } from './registration.service';
import { RegistrationTransitionService } from './registration-transition.service';
import { RegistrationData, saoPauloToday, validateRegistration } from './registration-validation';

@Component({ selector: 'app-registration-completion', imports: [FormsModule, RouterLink, RegistrationFormComponent], template: `
<section class="panel" aria-labelledby="completion-title">
  <h2 id="completion-title">Completar cadastro existente</h2>
  @if (loading()) { <p role="status">Verificando autorização…</p> }
  @if (message()) { <p role="alert">{{ message() }}</p> }
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
  private readonly transition = inject(RegistrationTransitionService);
  private readonly registration = inject(RegistrationService);
  private readonly router = inject(Router);
  readonly loading = signal(true); readonly busy = signal(false); readonly authorized = signal(false);
  readonly message = signal(''); readonly errors = signal<Record<string, string>>({});
  username = ''; legacyPassword = '';
  data: RegistrationData = { fullName: '', nickname: '', birthDate: '', email: '', phoneNumber: '', password: '' };

  async ngOnInit() {
    try {
      const state = (await this.registration.state()).state;
      if (state === 'completed') { await this.router.navigate(['/login'], { queryParams: { registered: true } }); return; }
      if (state === 'empty') { await this.router.navigate(['/register']); return; }
      try { await this.transition.authorization(); this.authorized.set(true); } catch { this.authorized.set(false); }
    } catch { this.message.set('Não foi possível verificar o cadastro.'); }
    finally { this.loading.set(false); }
  }

  ngOnDestroy() { this.legacyPassword = ''; this.data.password = ''; }

  async prove() {
    if (this.busy()) return;
    this.busy.set(true); this.message.set('');
    try { await this.transition.prove(this.username, this.legacyPassword); this.authorized.set(true); }
    catch (error) { this.message.set(this.messageFor(error, 'Não foi possível comprovar o acesso.')); }
    finally { this.legacyPassword = ''; this.busy.set(false); }
  }

  async submit() {
    if (this.busy()) return;
    const errors = validateRegistration(this.data, saoPauloToday()); this.errors.set(errors);
    if (Object.keys(errors).length) { this.message.set('Confira os campos informados.'); return; }
    this.busy.set(true); this.message.set('');
    try {
      await this.transition.complete(this.data);
      this.data.password = '';
      await this.router.navigate(['/login'], { queryParams: { registered: true } });
    } catch (error) {
      const response = error instanceof HttpErrorResponse ? error.error : null;
      if (response?.errors && typeof response.errors === 'object') this.errors.set(Object.fromEntries(Object.entries(response.errors).map(([key, value]) => [key, Array.isArray(value) ? String(value[0]) : 'Confira o valor informado.'])));
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.authorized.set(false);
        this.data.password = '';
        this.message.set('A autorização não está mais válida. Comprove o acesso novamente.');
      } else {
        this.message.set(this.messageFor(error, 'Não foi possível concluir o cadastro. Tente novamente.'));
      }
      if (error instanceof HttpErrorResponse && (error.status === 0 || error.status === 409 || error.status >= 500)) {
        try { if ((await this.registration.state()).state === 'completed') { this.data.password = ''; await this.router.navigate(['/login'], { queryParams: { registered: true } }); } } catch { /* manter formulário */ }
      }
    } finally { this.busy.set(false); }
  }

  private messageFor(error: unknown, fallback: string) {
    return error instanceof HttpErrorResponse && typeof error.error?.title === 'string' ? error.error.title : fallback;
  }
}
