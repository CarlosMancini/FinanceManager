import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { AuthService, requireSession, sessionExpiredInterceptor } from './auth.service';

function setup() {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionExpiredInterceptor])), provideHttpClientTesting()] });
  return { auth: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController), router: TestBed.inject(Router) };
}

describe('AuthService', () => {
  it('restores an existing session by email and rejects a missing session', async () => {
    const { auth, http } = setup();
    const restored = auth.restore(); http.expectOne('/api/auth/session').flush({ email: 'titular@example.test' });
    expect(await restored).toBe(true); expect(auth.email()).toBe('titular@example.test');
    const missing = auth.restore(); http.expectOne('/api/auth/session').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await missing).toBe(false); expect(auth.email()).toBeNull(); http.verify();
  });

  it('uses email and renews CSRF around login before restoring the session', async () => {
    const { auth, http } = setup();
    const result = auth.login('titular@example.test', 'senha');
    http.expectOne('/api/auth/csrf').flush(null); await new Promise(resolve => setTimeout(resolve, 0));
    const request = http.expectOne('/api/auth/login');
    expect(request.request.body).toEqual({ email: 'titular@example.test', password: 'senha' });
    request.flush(null); await new Promise(resolve => setTimeout(resolve, 0));
    http.expectOne('/api/auth/csrf').flush(null); await new Promise(resolve => setTimeout(resolve, 0));
    http.expectOne('/api/auth/session').flush({ email: 'titular@example.test' });
    await result; expect(auth.email()).toBe('titular@example.test'); http.verify();
  });

  it('blocks access through the session guard when authentication is missing', async () => {
    const { http, router } = setup();
    const result = TestBed.runInInjectionContext(() => requireSession({} as never, {} as never));
    http.expectOne('/api/auth/session').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(router.serializeUrl(await result as never)).toBe('/login'); http.verify();
  });

  it('expires on financial 401 including bills, but not on a failed login', async () => {
    const { auth, http, router } = setup();
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const failed = auth.login('a@b', 'senha').catch(() => undefined);
    http.expectOne('/api/auth/csrf').flush(null); await new Promise(resolve => setTimeout(resolve, 0));
    http.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    await failed; expect(navigate).not.toHaveBeenCalled();
    const client = TestBed.inject((await import('@angular/common/http')).HttpClient);
    client.get('/api/bills').subscribe({ error: () => undefined });
    http.expectOne('/api/bills').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { expired: true } }); http.verify();
  });
});
