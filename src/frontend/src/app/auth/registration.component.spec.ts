import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { RegistrationComponent } from './registration.component';
import { RegistrationService } from './registration.service';

describe('RegistrationComponent', () => {
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
    await fixture.componentInstance.ngOnInit();
    const component = fixture.componentInstance;
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b', phoneNumber: '11999999999', password: 'SenhaValida!123' };
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
    component.data = { fullName: 'Pessoa', nickname: '', birthDate: '1990-01-01', email: 'a@b', phoneNumber: '11999999999', password: 'SenhaValida!123' };
    await component.submit();
    expect(component.data.password).toBe('');
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { registered: true } });
  });
});
