import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { RegistrationTransitionService } from './registration-transition.service';

describe('RegistrationTransitionService', () => {
  it('obtains CSRF, submits the old credentials, and keeps only the expiry in memory', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(RegistrationTransitionService);
    const http = TestBed.inject(HttpTestingController);
    const result = service.prove('legado', 'senha');
    http.expectOne('/api/auth/csrf').flush(null);
    await new Promise(resolve => setTimeout(resolve, 0));
    const request = http.expectOne('/api/auth/registration/proof');
    expect(request.request.body).toEqual({ username: 'legado', password: 'senha' });
    request.flush({ expiresAtUtc: '2026-11-12T12:10:00Z' });
    await result;
    expect(service.expiresAtUtc).toBe('2026-11-12T12:10:00Z'); http.verify();
  });
});
