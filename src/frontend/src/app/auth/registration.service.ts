import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RegistrationData } from './registration-validation';

@Injectable({ providedIn: 'root' })
export class RegistrationService {
  private readonly http = inject(HttpClient);
  state() { return firstValueFrom(this.http.get<{ state: 'empty' | 'legacyPending' | 'completed' }>('/api/auth/registration-state')); }
  async register(data: RegistrationData) {
    await firstValueFrom(this.http.get('/api/auth/csrf'));
    return firstValueFrom(this.http.post<{ message: string }>('/api/auth/register', data));
  }
}
