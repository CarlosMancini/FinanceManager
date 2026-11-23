import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { routes } from '../app.routes';
import { AuthService, requireSession, sessionExpiredInterceptor } from './auth.service';
import { OwnerProfileComponent } from './owner-profile.component';

describe('profile navigation', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('requires only a session regardless of account availability', () => {
    const route = routes.find(item => item.path === 'profile');
    expect(route?.component).toBe(OwnerProfileComponent);
    expect(route?.canActivate).toEqual([requireSession]);
  });

  it('redirects to login when either profile request expires', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionExpiredInterceptor])), provideHttpClientTesting()] });
    const auth = TestBed.inject(AuthService); const client = TestBed.inject(HttpClient);
    const http = TestBed.inject(HttpTestingController); const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    auth.setProfileEmail('pessoa@example.test');
    client.get('/api/auth/profile').subscribe({ error: () => undefined });
    http.expectOne('/api/auth/profile').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { expired: true } });
    auth.setProfileEmail('pessoa@example.test');
    client.patch('/api/auth/profile', {}).subscribe({ error: () => undefined });
    http.expectOne('/api/auth/profile').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).toHaveBeenCalledTimes(2); http.verify();
  });
});
