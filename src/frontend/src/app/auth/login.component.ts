import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <section class="panel" aria-labelledby="login-title">
      <h2 id="login-title">Entrar</h2>
      @if (expired) { <p role="status">Sua sessão expirou. Entre novamente.</p> }
      @if (unavailable) { <p role="status">Não foi possível verificar sua sessão.</p> }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <form (ngSubmit)="submit()">
        <label>Usuário <input name="username" [(ngModel)]="username" required autocomplete="username"></label>
        <label>Senha <input name="password" type="password" [(ngModel)]="password" required autocomplete="current-password"></label>
        <button type="submit" [disabled]="busy() || !username || !password">{{ busy() ? 'Entrando…' : 'Entrar' }}</button>
      </form>
    </section>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly expired = this.route.snapshot.queryParamMap.has('expired');
  readonly unavailable = this.route.snapshot.queryParamMap.has('unavailable');
  readonly error = signal('');
  readonly busy = signal(false);
  username = '';
  password = '';

  async submit(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.username, this.password);
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
