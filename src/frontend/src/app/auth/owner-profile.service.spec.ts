import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OwnerProfileService, profileChanges } from './owner-profile.service';

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

  it('omits a change in case for common accented email characters', () => {
    const unicode = { ...original, email: 'josé@exemplo.test' };
    expect(profileChanges(unicode, { ...unicode, email: ' JOSÉ@EXEMPLO.TEST ' })).toEqual({ changes: {}, originalValues: {} });
  });

  it('omits fields restored to the original reference', () => {
    const edited = { ...original, fullName: 'Novo Nome', nickname: null };
    expect(Object.keys(profileChanges(original, edited).changes)).toEqual(['fullName', 'nickname']);
    expect(profileChanges(original, { ...edited, fullName: original.fullName, nickname: original.nickname })).toEqual({ changes: {}, originalValues: {} });
  });

  it('does not collapse Unicode uppercase expansions into a different email', () => {
    const unicode = { ...original, email: 'straße@example.test' };
    expect(profileChanges(unicode, { ...unicode, email: 'strasse@example.test' })).toEqual({
      changes: { email: 'strasse@example.test' }, originalValues: { email: 'straße@example.test' }
    });
  });

  it('matches invariant Greek casing and dotless i', () => {
    const greek = { ...original, email: 'ᾀ@example.test' };
    expect(profileChanges(greek, { ...greek, email: 'ᾈ@EXAMPLE.TEST' })).toEqual({ changes: {}, originalValues: {} });
    const dotless = { ...original, email: 'ı@example.test' };
    expect(profileChanges(dotless, { ...dotless, email: 'I@example.test' }).changes).toEqual({ email: 'I@example.test' });
  });
});

describe('OwnerProfileService', () => {
  const profile = { fullName: 'Pessoa', nickname: 'Nina', birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999' };
  let service: OwnerProfileService; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(OwnerProfileService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); TestBed.resetTestingModule(); });

  it('sends an isolated password change without personal originals', async () => {
    const password = { currentPassword: 'Atual', newPassword: 'Nova', confirmPassword: 'Nova' };
    const pending = service.update(profile, profile, password);
    const request = http.expectOne('/api/auth/profile');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ changes: {}, originalValues: {}, passwordChange: password });
    request.flush({ outcome: 'updated', message: 'Atualizado', profile });
    expect((await pending).outcome).toBe('updated');
  });

  it('does not retry conflict and sends matching keys for a combined update', async () => {
    const password = { currentPassword: 'Atual', newPassword: 'Nova', confirmPassword: 'Nova' };
    const pending = service.update(profile, { ...profile, fullName: 'Novo' }, password);
    const rejected = expect(pending).rejects.toMatchObject({ status: 409 });
    const request = http.expectOne('/api/auth/profile');
    expect(request.request.body).toEqual({ changes: { fullName: 'Novo' }, originalValues: { fullName: 'Pessoa' }, passwordChange: password });
    request.flush({ code: 'PROFILE_UPDATE_CONFLICT' }, { status: 409, statusText: 'Conflict' });
    await rejected;
    http.expectNone('/api/auth/profile');
  });
});
