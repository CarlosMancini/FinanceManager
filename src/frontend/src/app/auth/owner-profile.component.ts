import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from './auth.service';
import { OwnerProfile, OwnerProfileService, PasswordChange, profileChanges } from './owner-profile.service';
import { formatPhone, saoPauloToday, validateRegistration } from './registration-validation';

@Component({ selector: 'app-owner-profile', imports: [FormsModule], template: `
<section class="panel" aria-labelledby="profile-title">
  <h2 id="profile-title">Atualizar Cadastro</h2>
  @if (loading()) { <p role="status">Carregando cadastro…</p> }
  @if (message()) { <p role="alert">{{ message() }}</p> }
  @if (!loading() && !original) { <button type="button" (click)="load()">Tentar novamente</button> }
  @if (original && data) {
    <form (ngSubmit)="submit()" novalidate>
      <label>Nome completo <input name="fullName" [(ngModel)]="data.fullName" aria-describedby="fullName-error"></label><span id="fullName-error">{{ errors()['fullName'] }}</span>
      <label>Apelido (opcional) <input name="nickname" [(ngModel)]="data.nickname"></label>
      <label>Data de nascimento <input name="birthDate" type="date" [(ngModel)]="data.birthDate" aria-describedby="birthDate-error"></label><span id="birthDate-error">{{ errors()['birthDate'] }}</span>
      <label>E-mail <input name="email" type="email" [(ngModel)]="data.email" aria-describedby="email-error"></label><span id="email-error">{{ errors()['email'] }}</span>
      <label>Telefone <input name="phoneNumber" type="tel" inputmode="numeric" placeholder="(DDD) + Telefone" [ngModel]="data.phoneNumber" (ngModelChange)="changePhone($event)" aria-describedby="phoneNumber-error"></label><span id="phoneNumber-error">{{ errors()['phoneNumber'] }}</span>
      @if (!changingPassword()) { <button type="button" (click)="changingPassword.set(true)">Alterar senha</button> }
      @if (changingPassword()) {
        <p id="password-criteria">De 12 a 128 caracteres; inclua maiúscula, minúscula, número e símbolo.</p>
        <label>Senha atual <input name="currentPassword" type="password" autocomplete="current-password" [(ngModel)]="password.currentPassword" aria-describedby="currentPassword-error"></label><span id="currentPassword-error">{{ errors()['currentPassword'] }}</span>
        <label>Nova senha <input name="newPassword" type="password" autocomplete="new-password" [(ngModel)]="password.newPassword" aria-describedby="password-criteria newPassword-error"></label><span id="newPassword-error">{{ errors()['newPassword'] }}</span>
        <label>Confirmar nova senha <input name="confirmPassword" type="password" autocomplete="new-password" [(ngModel)]="password.confirmPassword" aria-describedby="confirmPassword-error"></label><span id="confirmPassword-error">{{ errors()['confirmPassword'] }}</span>
      }
      @if (conflict()) { <button type="button" (click)="review()" [disabled]="busy()">Revisar dados atuais</button> }
      @if (reviewData) {
        <p>Compare os valores atuais com os informados e escolha como continuar.</p>
        @for (field of conflictFields; track field) {
          <p>{{ fieldLabel(field) }}: atual: {{ reviewData[field] || 'Não informado' }}; informado: {{ data[field] || 'Não informado' }}</p>
          <label><input type="radio" [name]="'choice-' + field" [value]="'current'" [(ngModel)]="reviewChoice[field]"> Usar valor atual</label>
          <label><input type="radio" [name]="'choice-' + field" [value]="'draft'" [(ngModel)]="reviewChoice[field]"> Reaplicar valor informado</label>
        }
        @if (reviewError) { <p role="alert">Escolha como resolver cada campo alterado.</p> }
        <button type="button" (click)="applyReview()">Confirmar revisão</button>
      }
      <button type="submit" [disabled]="busy() || conflict()">{{ busy() ? 'Atualizando…' : 'Atualizar cadastro' }}</button>
    </form>
  }
</section>` })
export class OwnerProfileComponent implements OnInit, OnDestroy {
  private readonly service = inject(OwnerProfileService);
  private readonly auth = inject(AuthService);
  private readonly element: ElementRef<HTMLElement> = inject(ElementRef);
  readonly loading = signal(false); readonly busy = signal(false); readonly conflict = signal(false);
  readonly changingPassword = signal(false); readonly message = signal(''); readonly errors = signal<Record<string, string>>({});
  original: OwnerProfile | null = null; data: OwnerProfile | null = null; reviewData: OwnerProfile | null = null;
  conflictFields: (keyof OwnerProfile)[] = []; reviewChoice: Partial<Record<keyof OwnerProfile, 'current' | 'draft'>> = {}; reviewError = false;
  password: PasswordChange = { currentPassword: '', newPassword: '', confirmPassword: '' };

  ngOnInit() { void this.load(); }
  ngOnDestroy() { this.clearPassword(); }
  async load() {
    this.loading.set(true); this.message.set('');
    try { const profile = await this.service.get(); this.original = { ...profile }; this.data = { ...profile, phoneNumber: formatPhone(profile.phoneNumber) }; }
    catch { this.message.set('Não foi possível carregar o cadastro. Tente novamente.'); }
    finally { this.loading.set(false); }
  }
  changePhone(value: string) { if (this.data) this.data.phoneNumber = formatPhone(value); }
  async submit() {
    if (!this.original || !this.data || this.busy() || this.conflict()) return;
    const validation = validateRegistration({ ...this.data, nickname: this.data.nickname ?? '', password: this.password.newPassword, confirmPassword: this.password.confirmPassword }, saoPauloToday());
    if (!this.changingPassword()) { delete validation['password']; delete validation['confirmPassword']; }
    else {
      if (!this.password.currentPassword) validation['currentPassword'] = 'Informe a senha atual.';
      if (validation['password']) { validation['newPassword'] = validation['password']; delete validation['password']; }
    }
    this.errors.set(validation);
    if (Object.keys(validation).length) {
      this.message.set('Confira os campos informados.');
      const first = ['fullName', 'birthDate', 'email', 'phoneNumber', 'currentPassword', 'newPassword', 'confirmPassword'].find(field => validation[field]);
      if (first) this.element.nativeElement.querySelector<HTMLInputElement>(`input[name="${first}"]`)?.focus();
      return;
    }
    this.busy.set(true); this.message.set('');
    try {
      const response = await this.service.update(this.original, this.data, this.changingPassword() ? this.password : undefined);
      this.original = { ...response.profile }; this.data = { ...response.profile, phoneNumber: formatPhone(response.profile.phoneNumber) };
      this.clearPassword(); this.changingPassword.set(false); this.message.set(response.message);
      try { this.auth.setProfileEmail(response.profile.email); }
      catch { this.message.set(`${response.message} Atualize a página para sincronizar a exibição.`); }
    } catch (error) {
      const response = error instanceof HttpErrorResponse ? error.error : null;
      if (response?.errors) this.errors.set(Object.fromEntries(Object.entries(response.errors).map(([key, value]) => [key, Array.isArray(value) ? String(value[0]) : 'Confira o valor informado.'])));
      if (response?.code === 'PROFILE_UPDATE_CONFLICT' || !(error instanceof HttpErrorResponse) || error.status === 0 || error.status >= 500) {
        this.conflict.set(true);
        const fields = Object.keys(response?.errors ?? {}).filter((key): key is keyof OwnerProfile => ['fullName', 'nickname', 'birthDate', 'email', 'phoneNumber'].includes(key));
        this.conflictFields = fields.length ? fields : Object.keys(profileChanges(this.original, this.data).changes) as (keyof OwnerProfile)[];
      }
      this.message.set(typeof response?.title === 'string' ? response.title : 'Não foi possível atualizar o cadastro. Consulte os dados antes de tentar novamente.');
    } finally { this.busy.set(false); }
  }
  async review() {
    this.clearPassword(); this.busy.set(true); this.message.set('');
    try { this.reviewData = await this.service.get(); this.reviewChoice = {}; this.reviewError = false; }
    catch { this.message.set('Não foi possível consultar os dados atuais. Tente novamente.'); }
    finally { this.busy.set(false); }
  }
  applyReview() {
    if (!this.reviewData || !this.data || !this.original) return;
    if (this.conflictFields.some(field => !this.reviewChoice[field])) { this.reviewError = true; return; }
    this.reviewError = false;
    const next = { ...this.data };
    const edited = profileChanges(this.original, this.data).changes;
    for (const key of this.conflictFields) if (this.reviewChoice[key] === 'current') (next as unknown as Record<string, string | null>)[key] = this.reviewData[key];
    for (const key of ['fullName', 'nickname', 'birthDate', 'email', 'phoneNumber'] as const) {
      if (!this.conflictFields.includes(key) && !(key in edited)) (next as unknown as Record<string, string | null>)[key] = this.reviewData[key];
    }
    this.original = { ...this.reviewData }; this.data = { ...next, phoneNumber: formatPhone(next.phoneNumber) };
    this.reviewData = null; this.conflict.set(false); this.errors.set({}); this.message.set('Revise os dados e envie novamente.');
  }
  fieldLabel(field: keyof OwnerProfile): string { return ({ fullName: 'Nome completo', nickname: 'Apelido', birthDate: 'Data de nascimento', email: 'E-mail', phoneNumber: 'Telefone' })[field]; }
  private clearPassword() { this.password = { currentPassword: '', newPassword: '', confirmPassword: '' }; }
}
