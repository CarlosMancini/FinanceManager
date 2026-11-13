import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { RegistrationCompletionComponent } from './registration-completion.component';
import { RegistrationService } from './registration.service';
import { RegistrationTransitionService } from './registration-transition.service';

describe('RegistrationCompletionComponent', () => {
  it('checks availability after a lost response and directs a completed registration to login', async () => {
    const state = vi.fn().mockResolvedValueOnce({ state: 'legacyPending' }).mockResolvedValueOnce({ state: 'completed' });
    const transition = { authorization: vi.fn().mockResolvedValue(undefined), complete: vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 0 })) };
    await TestBed.configureTestingModule({ imports: [RegistrationCompletionComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state } }, { provide: RegistrationTransitionService, useValue: transition }] }).compileComponents();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const component = TestBed.createComponent(RegistrationCompletionComponent).componentInstance;
    await component.ngOnInit();
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999', password: 'SenhaValida!123' };
    await component.submit();
    expect(state).toHaveBeenCalledTimes(2);
    expect(component.data.password).toBe('');
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { registered: true } });
  });
  it('requires proof, preserves non-secret data on expiry, and allows new proof', async () => {
    const transition = { authorization: vi.fn().mockRejectedValue({}), prove: vi.fn().mockResolvedValue(undefined),
      complete: vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 401, error: { code: 'REGISTRATION_AUTHORIZATION_EXPIRED' } })) };
    await TestBed.configureTestingModule({ imports: [RegistrationCompletionComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'legacyPending' }) } },
      { provide: RegistrationTransitionService, useValue: transition }] }).compileComponents();
    const component = TestBed.createComponent(RegistrationCompletionComponent).componentInstance;
    await component.ngOnInit();
    expect(component.authorized()).toBe(false);
    component.username = 'legado'; component.legacyPassword = 'senha antiga';
    await component.prove();
    expect(component.authorized()).toBe(true); expect(component.legacyPassword).toBe('');
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999', password: 'SenhaValida!123' };
    await component.submit();
    expect(component.authorized()).toBe(false); expect(component.data.fullName).toBe('Pessoa'); expect(component.data.password).toBe('');
    component.legacyPassword = 'senha antiga'; await component.prove();
    expect(component.authorized()).toBe(true); expect(component.data.email).toBe('pessoa@example.test');
    component.ngOnDestroy(); expect(component.legacyPassword).toBe('');
  });

  it('returns to login after confirmed completion and discards the new password', async () => {
    const transition = { authorization: vi.fn().mockResolvedValue(undefined), complete: vi.fn().mockResolvedValue(undefined) };
    await TestBed.configureTestingModule({ imports: [RegistrationCompletionComponent], providers: [provideRouter([]),
      { provide: RegistrationService, useValue: { state: vi.fn().mockResolvedValue({ state: 'legacyPending' }) } },
      { provide: RegistrationTransitionService, useValue: transition }] }).compileComponents();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const component = TestBed.createComponent(RegistrationCompletionComponent).componentInstance;
    await component.ngOnInit();
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999', password: 'SenhaValida!123' };
    await component.submit();
    expect(component.data.password).toBe('');
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { registered: true } });
  });
});
