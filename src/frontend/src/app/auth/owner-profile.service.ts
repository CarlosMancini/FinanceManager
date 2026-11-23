import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { dotnetInvariantUpper, dotnetTrim } from './owner-profile-normalization';

export interface OwnerProfile { fullName: string; nickname: string | null; birthDate: string; email: string; phoneNumber: string; }
export interface PasswordChange { currentPassword: string; newPassword: string; confirmPassword: string; }
export interface ProfileResponse { outcome: 'updated' | 'unchanged'; message: string; profile: OwnerProfile; }

export function profileChanges(original: OwnerProfile, desired: OwnerProfile) {
  const changes: Record<string, string | null> = {};
  const originalValues: Record<string, string | null> = {};
  const keys: (keyof OwnerProfile)[] = ['fullName', 'nickname', 'birthDate', 'email', 'phoneNumber'];
  for (const key of keys) {
    const normalize = (value: string | null) => {
      if (key === 'phoneNumber') return (value ?? '').replace(/[^0-9]/g, '');
      if (key === 'email') return dotnetInvariantUpper(dotnetTrim(value ?? ''));
      if (key === 'nickname') return dotnetTrim(value ?? '') || null;
      return dotnetTrim(value ?? '');
    };
    if (normalize(original[key]) !== normalize(desired[key])) {
      changes[key] = key === 'nickname' ? dotnetTrim(desired.nickname ?? '') || null : desired[key] as string;
      originalValues[key] = original[key];
    }
  }
  return { changes, originalValues };
}

@Injectable({ providedIn: 'root' })
export class OwnerProfileService {
  private readonly http = inject(HttpClient);
  get(): Promise<OwnerProfile> { return firstValueFrom(this.http.get<OwnerProfile>('/api/auth/profile')); }
  update(original: OwnerProfile, desired: OwnerProfile, passwordChange?: PasswordChange): Promise<ProfileResponse> {
    return firstValueFrom(this.http.patch<ProfileResponse>('/api/auth/profile', {
      ...profileChanges(original, desired), ...(passwordChange ? { passwordChange } : {})
    }));
  }
}
