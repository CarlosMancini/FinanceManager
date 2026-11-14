import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';
import { RegistrationService } from './registration.service';
import { RegistrationTransitionService } from './registration-transition.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel" aria-labelledby="login-title">
      <h2 id="login-title">Entrar</h2>
      @if (expired) { <p role="status">Sua sessão expirou. Entre novamente.</p> }
      @if (unavailable) { <p role="status">Não foi possível verificar sua sessão.</p> }
      @if (registered) { <p role="status">Cadastro concluído. Entre para continuar.</p> }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <form (ngSubmit)="submit()">
        <label>E-mail <input name="email" type="email" [(ngModel)]="email" required autocomplete="username"></label>
        <label>Senha <input name="password" type="password" [(ngModel)]="password" required autocomplete="current-password"></label>
        <button type="submit" [disabled]="busy() || !email || !password">{{ busy() && !proving() ? 'Entrando…' : 'Entrar' }}</button>
      </form>
      @if (legacyPending()) {
        <button type="button" (click)="showLegacy.set(!showLegacy())">Usar acesso antigo</button>
        @if (showLegacy()) {
          <form (ngSubmit)="proveLegacy()">
            <label>Nome de acesso anterior <input name="legacyUsername" [(ngModel)]="legacyUsername" autocomplete="username" required></label>
            <label>Senha anterior <input name="legacyPassword" type="password" [(ngModel)]="legacyPassword" autocomplete="current-password" required></label>
            <button type="submit" [disabled]="busy() || !legacyUsername || !legacyPassword">{{ proving() ? 'Comprovando…' : 'Comprovar acesso' }}</button>
          </form>
        }
      }
      <p><a routerLink="/register">Criar conta</a></p>
    </section>
  `,
})
export class LoginComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly registration = inject(RegistrationService);
  private readonly transition = inject(RegistrationTransitionService);
  readonly expired = this.route.snapshot.queryParamMap.has('expired');
  readonly unavailable = this.route.snapshot.queryParamMap.has('unavailable');
  readonly registered = this.route.snapshot.queryParamMap.has('registered');
  readonly error = signal('');
  readonly busy = signal(false);
  readonly proving = signal(false);
  readonly legacyPending = signal(false);
  readonly showLegacy = signal(false);
  email = '';
  password = '';
  legacyUsername = '';
  legacyPassword = '';

  async ngOnInit() {
    try { this.legacyPending.set((await this.registration.state()).state === 'legacyPending'); }
    catch { /* o login permanece disponível */ }
  }

  ngOnDestroy() { this.password = ''; this.legacyPassword = ''; }

  async proveLegacy() {
    if (this.busy()) return;
    this.busy.set(true); this.proving.set(true); this.error.set('');
    try {
      await this.transition.prove(this.legacyUsername, this.legacyPassword);
      await this.router.navigate(['/register/complete']);
    } catch (error) {
      this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
        ? error.error.title : 'Não foi possível comprovar o acesso.');
    } finally { this.legacyPassword = ''; this.proving.set(false); this.busy.set(false); }
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.email, this.password);
      await this.router.navigateByUrl('/');
    } catch (error) {
      this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
        ? error.error.title : 'Não foi possível entrar. Tente novamente.');
    } finally {
      this.password = '';
      this.busy.set(false);
    }
  }
}
