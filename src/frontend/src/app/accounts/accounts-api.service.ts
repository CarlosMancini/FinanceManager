import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type AccountType = 'checking' | 'savings' | 'wallet';
export type AccountStatus = 'active' | 'archived';
export interface AccountDraft { name: string; type: AccountType }
export interface Account extends AccountDraft {
  id: string;
  currency: 'BRL';
  status: AccountStatus;
}
export interface AccountRevision { account: Account; version: string }

@Injectable({ providedIn: 'root' })
export class AccountsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/accounts';

  list(): Promise<Account[]> {
    return firstValueFrom(this.http.get<Account[]>(this.base));
  }

  async read(id: string): Promise<AccountRevision> {
    const response = await firstValueFrom(this.http.get<Account>(this.url(id), { observe: 'response' }));
    const version = response.headers.get('ETag');
    if (!response.body || !version) throw new Error('A resposta não contém a versão da conta.');
    return { account: response.body, version };
  }

  create(draft: AccountDraft): Promise<Account> {
    return firstValueFrom(this.http.post<Account>(this.base, draft));
  }

  edit(id: string, version: string, draft: AccountDraft): Promise<Account> {
    return firstValueFrom(this.http.put<Account>(this.url(id), draft, { headers: { 'If-Match': version } }));
  }

  changeStatus(id: string, version: string, status: AccountStatus): Promise<Account> {
    return firstValueFrom(this.http.put<Account>(this.url(id) + '/status', { status },
      { headers: { 'If-Match': version } }));
  }

  private url(id: string): string {
    return this.base + '/' + encodeURIComponent(id);
  }
}
