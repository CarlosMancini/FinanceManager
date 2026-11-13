import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { RouterLink, RouterOutlet, provideRouter } from '@angular/router';
import { routes } from './app/app.routes';
import { AuthService, sessionExpiredInterceptor } from './app/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  template: `
    <main>
      <header class="header">
        <h1>FinanceManager</h1>
        @if (auth.email()) {
          <nav class="actions"><a routerLink="/transactions">Movimentações</a><a routerLink="/bills">Contas a pagar</a><a routerLink="/accounts">Contas</a><button type="button" (click)="logout()" [disabled]="busy()">Sair</button></nav>
        }
      </header>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <router-outlet />
    </main>
  `,
})
class AppComponent {
  readonly auth = inject(AuthService);
  readonly busy = signal(false);
  readonly error = signal('');

  async logout(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.logout();
    } catch {
      if (this.auth.email()) this.error.set('Não foi possível sair. Tente novamente.');
    } finally {
      this.busy.set(false);
    }
  }
}

bootstrapApplication(AppComponent, {
  providers: [provideRouter(routes), provideHttpClient(withInterceptors([sessionExpiredInterceptor]))],
});
