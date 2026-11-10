import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type TransactionKind = 'income' | 'expense';
export type TransactionStatus = 'active' | 'canceled';
export interface TransactionDraft {
  accountId: string;
  kind: TransactionKind;
  amount: string;
  transactionDate: string;
  description: string;
}
export interface Transaction extends TransactionDraft {
  id: string;
  currency: 'BRL';
  source: 'manual';
  status: TransactionStatus;
}
export interface TransactionRevision { transaction: Transaction; version: string }

@Injectable({ providedIn: 'root' })
export class TransactionCommandApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/transactions';

  async create(key: string, draft: TransactionDraft): Promise<TransactionRevision> {
    const response = await firstValueFrom(this.http.post<Transaction>(this.base, draft,
      { headers: { 'Idempotency-Key': key }, observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  async read(id: string): Promise<TransactionRevision> {
    const response = await firstValueFrom(this.http.get<Transaction>(this.url(id), { observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  async edit(id: string, version: string, draft: TransactionDraft): Promise<TransactionRevision> {
    const response = await firstValueFrom(this.http.put<Transaction>(this.url(id), draft,
      { headers: { 'If-Match': version }, observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  async cancel(id: string, version: string): Promise<TransactionRevision> {
    const response = await firstValueFrom(this.http.put<Transaction>(this.url(id) + '/status', { status: 'canceled' },
      { headers: { 'If-Match': version }, observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  private revision(transaction: Transaction | null, version: string | null): TransactionRevision {
    if (!transaction || !version) throw new Error('A resposta não contém a versão da movimentação.');
    return { transaction, version };
  }

  private url(id: string): string { return this.base + '/' + encodeURIComponent(id); }
}
