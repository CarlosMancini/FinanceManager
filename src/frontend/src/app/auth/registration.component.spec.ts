import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { RegistrationComponent } from './registration.component';
import { RegistrationService } from './registration.service';
import { HttpErrorResponse } from '@angular/common/http';

describe('RegistrationComponent', () => {
  it('renders immediately without querying whether any account exists', async () => {
    const state = vi.fn(() => new Promise(() => {}));
    const register = vi.fn();
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state, register } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-registration-form')).toBeTruthy();
    expect(state).not.toHaveBeenCalled();
    expect(register).not.toHaveBeenCalled();
  });
  it('keeps registration available with existing users and reports duplicate email on submission', async () => {
    const register = vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 409, error: { code: 'EMAIL_ALREADY_REGISTERED', title: 'Este e-mail já está registrado.' } }));
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'completed' }), register } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-registration-form')).toBeTruthy();
    expect(register).not.toHaveBeenCalled();
    const component = fixture.componentInstance;
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b', phoneNumber: '61998433486', password: 'SenhaValida!123', confirmPassword: 'SenhaValida!123' };
    await component.submit(); fixture.detectChanges();
    expect(register).toHaveBeenCalledOnce(); expect(component.errors()['email']).toContain('já está registrado');
    expect(component.data.email).toBe('a@b');
    expect(fixture.nativeElement.querySelector('app-registration-form')).toBeTruthy();
    component.ngOnDestroy(); expect(component.data.confirmPassword).toBe('');
  });
  it('moves focus to the error summary after invalid submission', async () => {
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'empty' }) } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.componentInstance.submit();
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve));
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role="alert"]'));
    expect(document.activeElement?.textContent).toContain('Confira os campos');
    fixture.nativeElement.remove();
  });
  it('offers the completion path for an existing legacy owner', async () => {
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'legacyPending' }) } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Completar cadastro existente');
    expect(fixture.nativeElement.querySelector('a[href="/register/complete"]')).toBeTruthy();
  });
  it('offers a return link to login', async () => {
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]), { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'empty' }), register: vi.fn() } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('a');
    expect(link.getAttribute('href')).toBe('/login');
  });

  it('preserves non-secret input on error and clears password on exit', async () => {
    const register = vi.fn().mockRejectedValue({});
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]), { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'empty' }), register } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    const component = fixture.componentInstance;
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b', phoneNumber: '11999999999', password: 'SenhaValida!123', confirmPassword: 'SenhaValida!123' };
    await component.submit();
    expect(register).toHaveBeenCalledOnce();
    expect(component.data.fullName).toBe('Pessoa');
    expect(component.message()).toContain('Não foi possível');
    component.ngOnDestroy();
    expect(component.data.password).toBe('');
  });

  it('returns to login after success without retaining the password', async () => {
    await TestBed.configureTestingModule({ imports: [RegistrationComponent], providers: [provideRouter([]), { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'empty' }), register: vi.fn().mockResolvedValue({}) } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegistrationComponent);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const component = fixture.componentInstance;
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b', phoneNumber: '11999999999', password: 'SenhaValida!123', confirmPassword: 'SenhaValida!123' };
    await component.submit();
    expect(component.data.password).toBe('');
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { registered: true } });
  });
});
