import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RegistrationData } from './registration-validation';

@Injectable({ providedIn: 'root' })
export class RegistrationTransitionService {
  private readonly http = inject(HttpClient);
  expiresAtUtc: string | null = null;

  async prove(username: string, password: string): Promise<void> {
    await this.csrf();
    const result = await firstValueFrom(this.http.post<{ expiresAtUtc: string }>('/api/auth/registration/proof', { username, password }));
    this.expiresAtUtc = result.expiresAtUtc;
  }

  async authorization(): Promise<void> {
    const result = await firstValueFrom(this.http.get<{ expiresAtUtc: string }>('/api/auth/registration/authorization'));
    this.expiresAtUtc = result.expiresAtUtc;
  }

  async complete(data: RegistrationData): Promise<void> {
    await this.csrf();
    await firstValueFrom(this.http.post('/api/auth/registration/complete', data));
    this.expiresAtUtc = null;
    await this.csrf();
  }

  private async csrf(): Promise<void> { await firstValueFrom(this.http.get('/api/auth/csrf')); }
}
