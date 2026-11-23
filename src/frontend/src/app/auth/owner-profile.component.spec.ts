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

  it('does not expose the form when the initial load fails and permits retry', async () => {
    get.mockRejectedValueOnce(new Error('rede'));
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.componentInstance.message()).toContain('Não foi possível carregar');
    await fixture.componentInstance.load(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(fixture.componentInstance.data!.fullName).toBe('Pessoa');
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

  it('focuses the first invalid field after submission', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const component = fixture.componentInstance; component.data!.fullName = ''; component.data!.email = '';
    await component.submit();
    expect(fixture.nativeElement.ownerDocument.activeElement).toBe(fixture.nativeElement.querySelector('input[name="fullName"]'));
    expect(update).not.toHaveBeenCalled();
  });

  it('clears passwords when review starts and preserves the draft if the review fails', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Meu Nome'; component.changingPassword.set(true);
    component.password = { currentPassword: 'Atual', newPassword: 'Nova', confirmPassword: 'Nova' };
    component.conflict.set(true); get.mockRejectedValueOnce(new Error('rede'));
    await component.review();
    expect(component.password).toEqual({ currentPassword: '', newPassword: '', confirmPassword: '' });
    expect(component.original!.fullName).toBe('Pessoa'); expect(component.data!.fullName).toBe('Meu Nome');
    expect(component.conflict()).toBe(true); expect(update).not.toHaveBeenCalled();
  });

  it('does not validate when opening or blurring passwords and clears them after success', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const component = fixture.componentInstance;
    fixture.nativeElement.querySelector('button[type="button"]').click(); fixture.detectChanges();
    const inputs = fixture.nativeElement.querySelectorAll('input[type="password"]');
    expect(inputs.length).toBe(3); inputs[0].dispatchEvent(new Event('blur')); fixture.detectChanges();
    expect(component.errors()).toEqual({}); expect(update).not.toHaveBeenCalled();
    component.password = { currentPassword: 'Senha-Antiga!123', newPassword: 'Senha-Nova!1234', confirmPassword: 'Senha-Nova!1234' };
    update.mockResolvedValue({ outcome: 'updated', message: 'Atualizado', profile });
    await component.submit(); fixture.detectChanges();
    expect(component.password).toEqual({ currentPassword: '', newPassword: '', confirmPassword: '' });
    expect(component.changingPassword()).toBe(false);
    expect(fixture.nativeElement.querySelectorAll('input[type="password"]').length).toBe(0);
  });

  it('clears passwords when the component is destroyed', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance;
    component.password = { currentPassword: 'Atual', newPassword: 'Nova', confirmPassword: 'Nova' };
    fixture.destroy();
    expect(component.password).toEqual({ currentPassword: '', newPassword: '', confirmPassword: '' });
  });

  it('does not report a failed save when updating the displayed email fails after commit', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Confirmado';
    update.mockResolvedValue({ outcome: 'updated', message: 'Cadastro atualizado com sucesso.', profile: { ...profile, fullName: 'Confirmado' } });
    setProfileEmail.mockImplementationOnce(() => { throw new Error('falha visual'); });
    await component.submit();
    expect(component.message()).toContain('Cadastro atualizado com sucesso.');
    expect(component.conflict()).toBe(false); expect(component.data!.fullName).toBe('Confirmado');
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

  it('adopts current value when explicitly selected and does not submit automatically', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Rascunho';
    update.mockRejectedValueOnce(new HttpErrorResponse({ status: 409, error: { code: 'PROFILE_UPDATE_CONFLICT', errors: { fullName: ['Alterado'] } } }));
    await component.submit(); get.mockResolvedValueOnce({ ...profile, fullName: 'Atual' }); await component.review();
    component.reviewChoice.fullName = 'current'; component.applyReview();
    expect(component.data!.fullName).toBe('Atual'); expect(component.original!.fullName).toBe('Atual');
    expect(update).toHaveBeenCalledTimes(1);
  });

  it.each([403, 429])('keeps the draft without automatic retry after status %s', async (status) => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Rascunho';
    update.mockRejectedValueOnce(new HttpErrorResponse({ status, error: { title: 'Tente novamente.' } }));
    await component.submit();
    expect(component.data!.fullName).toBe('Rascunho'); expect(component.original!.fullName).toBe('Pessoa');
    expect(component.conflict()).toBe(false); expect(update).toHaveBeenCalledTimes(1);
  });

  it.each([0, 500])('requires explicit review after uncertain status %s', async (status) => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Rascunho';
    update.mockRejectedValueOnce(new HttpErrorResponse({ status, error: {} }));
    await component.submit();
    expect(component.conflict()).toBe(true); expect(component.original!.fullName).toBe('Pessoa');
    expect(component.data!.fullName).toBe('Rascunho'); expect(get).toHaveBeenCalledTimes(1);
    expect(update).toHaveBeenCalledTimes(1);
  });

  it('rejects another conflicting change after the user reviews the first one', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.data!.fullName = 'Rascunho';
    const conflict = new HttpErrorResponse({ status: 409, error: { code: 'PROFILE_UPDATE_CONFLICT', errors: { fullName: ['Alterado'] } } });
    update.mockRejectedValue(conflict);
    await component.submit(); get.mockResolvedValueOnce({ ...profile, fullName: 'Atual um' }); await component.review();
    component.reviewChoice.fullName = 'draft'; component.applyReview(); await component.submit();
    expect(component.conflict()).toBe(true); expect(component.data!.fullName).toBe('Rascunho');
    expect(component.original!.fullName).toBe('Atual um'); expect(update).toHaveBeenCalledTimes(2);
  });
});
