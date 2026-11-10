import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, throwError } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly current = signal<string | null>(null);
  readonly username = this.current.asReadonly();

  async restore(): Promise<boolean> {
    try {
      const session = await firstValueFrom(this.http.get<{ username: string }>('/api/auth/session'));
      this.current.set(session.username);
      return true;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.current.set(null);
        return false;
      }
      throw error;
    }
  }

  async login(username: string, password: string): Promise<void> {
    await this.refreshCsrf();
    await firstValueFrom(this.http.post('/api/auth/login', { username, password }));
    await this.refreshCsrf();
    await this.restore();
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/logout', null));
    this.current.set(null);
    await this.router.navigate(['/login']);
    await this.refreshCsrf();
  }

  expire(): void {
    this.current.set(null);
    void this.router.navigate(['/login'], { queryParams: { expired: true } });
  }

  private async refreshCsrf(): Promise<void> {
    await firstValueFrom(this.http.get('/api/auth/csrf'));
  }
}

export const requireSession: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  try {
    return (await auth.restore()) || router.createUrlTree(['/login']);
  } catch {
    return router.createUrlTree(['/login'], { queryParams: { unavailable: true } });
  }
};

export const sessionExpiredInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  return next(request).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 401 &&
        (request.url.startsWith('/api/accounts') || request.url === '/api/auth/logout')) {
      auth.expire();
    }
    return throwError(() => error);
  }));
};
