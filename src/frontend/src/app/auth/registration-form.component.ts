import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RegistrationData, passwordCriteria } from './registration-validation';

@Component({ selector: 'app-registration-form', imports: [FormsModule], template: `
<form (ngSubmit)="submitted.emit()" novalidate>
  <label>Nome completo <input name="fullName" [(ngModel)]="data.fullName" required aria-describedby="fullName-error"></label><span id="fullName-error">{{ errors['fullName'] }}</span>
  <label>Apelido (opcional) <input name="nickname" [(ngModel)]="data.nickname"></label>
  <label>Data de nascimento <input name="birthDate" type="date" [(ngModel)]="data.birthDate" required aria-describedby="birthDate-error"></label><span id="birthDate-error">{{ errors['birthDate'] }}</span>
  <label>E-mail <input name="email" type="email" [(ngModel)]="data.email" required aria-describedby="email-error"></label><span id="email-error">{{ errors['email'] }}</span>
  <label>Telefone <input name="phoneNumber" type="tel" [(ngModel)]="data.phoneNumber" required aria-describedby="phoneNumber-error"></label><span id="phoneNumber-error">{{ errors['phoneNumber'] }}</span>
  <label>Senha <input name="password" type="password" autocomplete="new-password" [(ngModel)]="data.password" required aria-describedby="password-criteria password-error"></label>
  <div id="password-criteria">De 12 a 128 caracteres; inclua maiúscula, minúscula, número e símbolo.
    <ul><li>Comprimento: {{ criteria().length ? 'atendido' : 'não atendido' }}</li><li>Maiúscula: {{ criteria().uppercase ? 'atendido' : 'não atendido' }}</li><li>Minúscula: {{ criteria().lowercase ? 'atendido' : 'não atendido' }}</li><li>Número: {{ criteria().digit ? 'atendido' : 'não atendido' }}</li><li>Símbolo: {{ criteria().symbol ? 'atendido' : 'não atendido' }}</li></ul>
  </div><span id="password-error">{{ errors['password'] }}</span>
  <button type="submit" [disabled]="busy">{{ busy ? 'Salvando…' : 'Criar conta' }}</button>
</form>` })
export class RegistrationFormComponent {
  @Input({ required: true }) data!: RegistrationData;
  @Input() errors: Record<string, string> = {};
  @Input() busy = false;
  @Output() submitted = new EventEmitter<void>();
  criteria() { return passwordCriteria(this.data.password); }
}
