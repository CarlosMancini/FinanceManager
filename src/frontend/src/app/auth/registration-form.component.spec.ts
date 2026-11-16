import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { RegistrationFormComponent } from './registration-form.component';
import { formatPhone, validateRegistration } from './registration-validation';

describe('RegistrationFormComponent', () => {
  it('masks digit input and pastes while preserving excess digits for validation', async () => {
    const fixture = TestBed.createComponent(RegistrationFormComponent);
    const data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b', phoneNumber: '', password: 'SenhaValida!123', confirmPassword: 'SenhaValida!123' };
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    await fixture.whenStable();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('[name="phoneNumber"]');
    input.value = '61998433486'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    await fixture.whenStable(); fixture.detectChanges();
    expect(input.value).toBe('(61) 99843-3486');
    expect(data.phoneNumber).toBe('(61) 99843-3486');
    const letter = new KeyboardEvent('keydown', { key: 'a', cancelable: true });
    input.dispatchEvent(letter); expect(letter.defaultPrevented).toBe(true);
    expect(formatPhone('abc(61) 99843-3486')).toBe('(61) 99843-3486');
    data.phoneNumber = formatPhone('619984334860');
    expect(validateRegistration(data, '2026-11-16')['phoneNumber']).toBeTruthy();
  });

  it('renders two concealed password fields with associated errors', () => {
    const fixture = TestBed.createComponent(RegistrationFormComponent);
    fixture.componentRef.setInput('data', { password: '', confirmPassword: '' });
    fixture.componentRef.setInput('errors', { confirmPassword: 'As senhas devem ser idênticas.' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('input[type="password"]').length).toBe(2);
    expect(fixture.nativeElement.querySelector('#confirmPassword-error').textContent).toContain('idênticas');
  });
});
