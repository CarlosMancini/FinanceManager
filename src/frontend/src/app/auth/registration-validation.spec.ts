import { describe, expect, it } from 'vitest';
import { passwordCriteria, validateRegistration } from './registration-validation';

describe('registration validation', () => {
  const valid = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b.test', phoneNumber: '11999999999', password: 'SenhaValida!123', confirmPassword: 'SenhaValida!123' };
  it('requires non-empty domain and extension in the email', () => {
    for (const email of ['teste@gmail', 'teste@dominio.', 'teste@.com', 'teste@dominio..com', 'enderecoEmail@dominio.com,br'])
      expect(validateRegistration({ ...valid, email }, '2026-11-17')['email']).toBeTruthy();
    for (const email of ['enderecoEmail@dominio.com', 'enderecoEmail@dominio.com.br'])
      expect(validateRegistration({ ...valid, email }, '2026-11-17')['email']).toBeUndefined();
  });
  it('accepts optional nickname and valid fields', () => expect(validateRegistration(valid, '2026-11-12')).toEqual({}));
  it('rejects invalid required fields and dates', () => {
    const errors = validateRegistration({ ...valid, fullName: ' ', birthDate: '2026-02-31', email: 'a@@b', phoneNumber: '123', password: 'curta' }, '2026-11-12');
    expect(Object.keys(errors).sort()).toEqual(['birthDate', 'confirmPassword', 'email', 'fullName', 'password', 'phoneNumber']);
    expect(validateRegistration({ ...valid, birthDate: '2026-11-13' }, '2026-11-12')['birthDate']).toBeTruthy();
  });
  it('counts Unicode code points and checks each category', () => {
    expect(Object.values(passwordCriteria('Aa1!aaaaaaa😀')).every(Boolean)).toBe(true);
    expect(passwordCriteria('Aa1!aaaaaaa').length).toBe(false);
    expect(passwordCriteria('Aa1!' + 'a'.repeat(124)).length).toBe(true);
    expect(passwordCriteria('Aa1!' + 'a'.repeat(125)).length).toBe(false);
    expect(passwordCriteria('Aa١!aaaaaaaa').digit).toBe(false);
    expect(passwordCriteria('Aa1 aaaaaaaa').symbol).toBe(false);
  });
  it('enforces phone digit boundaries and rejects invalid symbols', () => {
    for (const phone of ['61998433486', '(61) 99843-3486'])
      expect(validateRegistration({ ...valid, phoneNumber: phone }, '2026-11-12')['phoneNumber']).toBeUndefined();
    for (const phone of ['1234567890', '123456789012', '+55 (11) 99999-9999', '123456789a', '12345+67890'])
      expect(validateRegistration({ ...valid, phoneNumber: phone }, '2026-11-12')['phoneNumber']).toBeTruthy();
  });
  it('requires valid identical passwords without trimming', () => {
    for (const confirmPassword of ['', 'OutraSenha!123', 'SenhaValida!123 '])
      expect(validateRegistration({ ...valid, confirmPassword }, '2026-11-12')['confirmPassword']).toBeTruthy();
    expect(validateRegistration({ ...valid, password: 'curta', confirmPassword: 'curta' }, '2026-11-12')['confirmPassword']).toBeTruthy();
  });
});
