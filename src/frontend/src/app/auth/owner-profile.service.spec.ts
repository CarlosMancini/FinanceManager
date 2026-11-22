import { describe, expect, it } from 'vitest';
import { profileChanges } from './owner-profile.service';

describe('profileChanges', () => {
  const original = { fullName: 'Pessoa', nickname: 'Nina', birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999' };

  it('sends only edited fields with their original values', () => {
    expect(profileChanges(original, { ...original, fullName: 'Novo Nome', nickname: null })).toEqual({
      changes: { fullName: 'Novo Nome', nickname: null },
      originalValues: { fullName: 'Pessoa', nickname: 'Nina' }
    });
  });

  it('omits equivalent formatted values', () => {
    expect(profileChanges(original, { ...original, email: ' PESSOA@EXAMPLE.TEST ', phoneNumber: '(11) 99999-9999' })).toEqual({ changes: {}, originalValues: {} });
  });
});
