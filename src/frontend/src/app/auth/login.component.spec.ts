import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';
import { RegistrationService } from './registration.service';
import { RegistrationTransitionService } from './registration-transition.service';

describe('LoginComponent', () => {
  it('offers legacy proof and navigates only to completion without keeping the old password', async () => {
    const prove = vi.fn().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({ imports: [LoginComponent], providers: [provideRouter([]),
      { provide: AuthService, useValue: { login: vi.fn() } },
      { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'legacyPending' }) } },
      { provide: RegistrationTransitionService, useValue: { prove } }] }).compileComponents();
    const fixture = TestBed.createComponent(LoginComponent);
    const component = fixture.componentInstance;
    await component.ngOnInit(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Usar acesso antigo');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component.legacyUsername = 'legado'; component.legacyPassword = 'senha anterior';
    await component.proveLegacy();
    expect(prove).toHaveBeenCalledWith('legado', 'senha anterior');
    expect(component.legacyPassword).toBe('');
    expect(navigate).toHaveBeenCalledWith(['/register/complete']);
  });
  it('submits email and clears the password before navigating to the protected landing page', async () => {
    const login = vi.fn().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({ imports: [LoginComponent], providers: [provideRouter([]), { provide: AuthService, useValue: { login } }, { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'empty' }) } }, { provide: RegistrationTransitionService, useValue: {} }] }).compileComponents();
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const component = fixture.componentInstance;
    component.email = 'titular@example.test'; component.password = 'senha';
    await component.submit();
    expect(login).toHaveBeenCalledWith('titular@example.test', 'senha');
    expect(component.password).toBe('');
    expect(navigate).toHaveBeenCalledWith('/');
    expect(fixture.nativeElement.querySelector('input[name=email]').type).toBe('email');
    expect(fixture.nativeElement.querySelector('a').getAttribute('href')).toBe('/register');
  });

  it('clears the password and displays the generic credential error', async () => {
    const login = vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 401, error: { title: 'Credenciais inválidas.' } }));
    await TestBed.configureTestingModule({ imports: [LoginComponent], providers: [provideRouter([]), { provide: AuthService, useValue: { login } }, { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'empty' }) } }, { provide: RegistrationTransitionService, useValue: {} }] }).compileComponents();
    const component = TestBed.createComponent(LoginComponent).componentInstance;
    component.email = 'titular@example.test'; component.password = 'incorreta';
    await component.submit();
    expect(component.password).toBe('');
    expect(component.email).toBe('titular@example.test');
    expect(component.error()).toBe('Credenciais inválidas.');
  });
});
