import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { OwnerProfileComponent } from './owner-profile.component';
import { OwnerProfileService } from './owner-profile.service';

describe('OwnerProfileComponent password', () => {
  const profile = { fullName: 'Pessoa', nickname: null, birthDate: '1990-01-01', email: 'pessoa@example.test', phoneNumber: '11999999999' };
  const get = vi.fn(); const update = vi.fn();
  beforeEach(async () => {
    TestBed.resetTestingModule(); get.mockReset(); update.mockReset(); get.mockResolvedValue({ ...profile });
    await TestBed.configureTestingModule({ imports: [OwnerProfileComponent], providers: [
      { provide: OwnerProfileService, useValue: { get, update } }, { provide: AuthService, useValue: { setProfileEmail: vi.fn() } }
    ] }).compileComponents();
  });

  it('shows exactly three empty password fields without validation on input or blur', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('input[type=password]').length).toBe(0);
    fixture.nativeElement.querySelector('button[type=button]').click(); fixture.detectChanges();
    const fields: NodeListOf<HTMLInputElement> = fixture.nativeElement.querySelectorAll('input[type=password]');
    expect(fields.length).toBe(3); expect(Array.from(fields).map(field => field.value)).toEqual(['', '', '']);
    fields[0].value = 'teste'; fields[0].dispatchEvent(new Event('input')); fields[0].dispatchEvent(new Event('blur')); fixture.detectChanges();
    expect(fixture.componentInstance.errors()).toEqual({}); expect(get).toHaveBeenCalledTimes(1); expect(update).not.toHaveBeenCalled();
  });

  it('requires the current password again after conflict review', async () => {
    const fixture = TestBed.createComponent(OwnerProfileComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance; component.changingPassword.set(true);
    component.password = { currentPassword: 'Atual', newPassword: 'Senha-Nova!1234', confirmPassword: 'Senha-Nova!1234' };
    component.data!.fullName = 'Rascunho';
    update.mockRejectedValue(new HttpErrorResponse({ status: 409, error: { code: 'PROFILE_UPDATE_CONFLICT', errors: { fullName: ['Alterado'] } } }));
    await component.submit(); get.mockResolvedValueOnce({ ...profile, fullName: 'Atual' }); await component.review();
    expect(component.password).toEqual({ currentPassword: '', newPassword: '', confirmPassword: '' });
    component.reviewChoice.fullName = 'draft'; component.applyReview(); await component.submit();
    expect(component.errors()['currentPassword']).toBeTruthy(); expect(update).toHaveBeenCalledTimes(1);
  });
});
