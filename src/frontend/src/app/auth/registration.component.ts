import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { RegistrationService } from './registration.service';
import { RegistrationData, saoPauloToday, validateRegistration } from './registration-validation';
import { RegistrationFormComponent } from './registration-form.component';

@Component({ selector: 'app-registration', imports: [RegistrationFormComponent, RouterLink], template: `
<section class="panel" aria-labelledby="registration-title">
  <h2 id="registration-title">Criar conta</h2>
  @if (loading()) { <p role="status">Verificando cadastro…</p> }
  <p #errorSummary role="alert" tabindex="-1" [hidden]="!message()">{{ message() }}</p>
  @if (state() === 'legacyPending') { <p>Existe um cadastro que precisa ser completado.</p><p><a routerLink="/register/complete">Completar cadastro existente</a></p> }
  @if (!loading()) {
    <app-registration-form [data]="data" [errors]="errors()" [busy]="busy()" (submitted)="submit()" />
  }
  <p><a routerLink="/login">Voltar ao login</a></p>
</section>` })
export class RegistrationComponent implements OnInit, OnDestroy {
  @ViewChild('errorSummary') private errorSummary?: ElementRef<HTMLElement>;
  private readonly service = inject(RegistrationService);
  private readonly router = inject(Router);
  readonly state = signal<'empty' | 'legacyPending' | 'completed' | null>(null);
  readonly loading = signal(true); readonly busy = signal(false); readonly message = signal('');
  readonly errors = signal<Record<string, string>>({});
  data: RegistrationData = { fullName: '', nickname: '', birthDate: '', email: '', phoneNumber: '', password: '', confirmPassword: '' };
  async ngOnInit() { try { this.state.set((await this.service.state()).state); } catch { this.message.set('Não foi possível verificar o cadastro.'); } finally { this.loading.set(false); } }
  ngOnDestroy() { this.clearPasswords(); }
  async submit() {
    if (this.busy()) return;
    const errors = validateRegistration(this.data, saoPauloToday()); this.errors.set(errors);
    if (Object.keys(errors).length) { this.message.set('Confira os campos informados.'); this.focusError(); return; }
    this.busy.set(true); this.message.set('');
    try { await this.service.register(this.data); this.clearPasswords(); await this.router.navigate(['/login'], { queryParams: { registered: true } }); }
    catch (error) {
      const response = error instanceof HttpErrorResponse ? error.error : null;
      if (response?.errors && typeof response.errors === 'object') this.errors.set(Object.fromEntries(Object.entries(response.errors).map(([key, value]) => [key, Array.isArray(value) ? String(value[0]) : 'Confira o valor informado.'])));
      if (response?.code === 'EMAIL_ALREADY_REGISTERED') this.errors.set({ email: 'Este e-mail já está registrado. Entre ou informe outro e-mail.' });
      this.message.set(typeof response?.title === 'string' ? response.title : 'Não foi possível concluir o cadastro. Tente novamente.');
      this.focusError();
      if (error instanceof HttpErrorResponse && (error.status === 409 || error.status === 0 || error.status >= 500)) { try { this.state.set((await this.service.state()).state); } catch { /* manter orientação de erro */ } }
    } finally { this.busy.set(false); }
  }
  private clearPasswords() { this.data.password = ''; this.data.confirmPassword = ''; }
  private focusError() { setTimeout(() => this.errorSummary?.nativeElement.focus()); }
}
