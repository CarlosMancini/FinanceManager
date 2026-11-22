import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { OwnerProfileComponent } from './owner-profile.component';
import { OwnerProfileService } from './owner-profile.service';

describe('OwnerProfileComponent', () => {
  const profile = { fullName: 'Pessoa', nickname: 'Nina', birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999' };
  const get = vi.fn(); const update = vi.fn(); const setProfileEmail = vi.fn();
  beforeEach(async () => {
    TestBed.resetTestingModule(); get.mockReset(); update.mockReset(); setProfileEmail.mockReset();
    get.mockResolvedValue({ ...profile });
    await TestBed.configureTestingModule({ imports: [OwnerProfileComponent], providers: [
      { provide: OwnerProfileService, useValue: { get, update } }, { provide: AuthService, useValue: { setProfileEmail } }
    ] }).compileComponents();
  });

  it('loads the profile and reveals empty password fields only on demand', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.componentInstance.data?.fullName).toBe('Pessoa');
    expect(fixture.nativeElement.querySelectorAll('input[type=password]').length).toBe(0);
    fixture.componentInstance.changingPassword.set(true); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('input[type=password]').length).toBe(3);
    expect(fixture.componentInstance.password.currentPassword).toBe('');
  });

  it('validates on submit and does not require a password for profile edits', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = '';
    expect(component.errors()).toEqual({}); await component.submit();
    expect(component.errors()['fullName']).toBeTruthy(); expect(update).not.toHaveBeenCalled();
    component.data!.fullName = 'Novo Nome'; update.mockResolvedValue({ outcome: 'updated', message: 'Cadastro atualizado.', profile: { ...profile, fullName: 'Novo Nome' } });
    await component.submit(); expect(update).toHaveBeenCalledWith(profile, expect.objectContaining({ fullName: 'Novo Nome' }), undefined);
    expect(setProfileEmail).toHaveBeenCalledWith(profile.email);
  });

  it('keeps the draft on conflict and requires explicit review', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Meu Nome';
    update.mockRejectedValue(new HttpErrorResponse({ status: 409, error: { code: 'PROFILE_UPDATE_CONFLICT', title: 'Revise os dados.', errors: { fullName: ['Alterado'] } } }));
    await component.submit(); expect(component.conflict()).toBe(true); expect(component.data!.fullName).toBe('Meu Nome');
    get.mockResolvedValue({ ...profile, fullName: 'Nome Atual' }); await component.review();
    expect(component.original!.fullName).toBe('Pessoa'); component.applyReview();
    expect(component.reviewError).toBe(true); expect(component.original!.fullName).toBe('Pessoa');
    component.reviewChoice.fullName = 'draft'; component.applyReview();
    expect(component.original!.fullName).toBe('Nome Atual'); expect(component.data!.fullName).toBe('Meu Nome'); expect(component.conflict()).toBe(false);
  });
});
