import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type BillStatus = 'upcoming' | 'dueToday' | 'overdue' | 'paid' | 'canceled';
export type BillFilterStatus = 'open' | BillStatus | 'all';
export interface BillDraft { description: string; amount: string; dueDate: string }
export interface Bill extends BillDraft {
  id: string;
  currency: 'BRL';
  status: BillStatus;
  activeSettlement: { id: string; transactionId: string; origin: 'linkedExpense' | 'createdExpense'; createdAtUtc: string } | null;
}
export interface BillRevision { bill: Bill; version: string }
export interface BillPage { items: Bill[]; page: number; pageSize: number; totalCount: number; totals: { openAmount: string } }
export interface BillFilters { status: BillFilterStatus; from?: string; to?: string; page: number; pageSize: number }

@Injectable({ providedIn: 'root' })
export class BillApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/bills';

  list(filters: BillFilters): Promise<BillPage> {
    let params = new HttpParams().set('status', filters.status).set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    return firstValueFrom(this.http.get<BillPage>(this.base, { params }));
  }

  async read(id: string): Promise<BillRevision> {
    const response = await firstValueFrom(this.http.get<Bill>(this.url(id), { observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  async create(draft: BillDraft): Promise<BillRevision> {
    const response = await firstValueFrom(this.http.post<Bill>(this.base, draft, { observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  async edit(id: string, version: string, draft: BillDraft): Promise<BillRevision> {
    const response = await firstValueFrom(this.http.put<Bill>(this.url(id), draft,
      { headers: { 'If-Match': version }, observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  async cancel(id: string, version: string): Promise<BillRevision> {
    const response = await firstValueFrom(this.http.put<Bill>(this.url(id) + '/status', { status: 'canceled' },
      { headers: { 'If-Match': version }, observe: 'response' }));
    return this.revision(response.body, response.headers.get('ETag'));
  }

  private revision(body: Bill | null, version: string | null): BillRevision {
    if (!body || !version) throw new Error('A resposta não contém a versão da obrigação.');
    return { bill: body, version };
  }

  private url(id: string): string { return this.base + '/' + encodeURIComponent(id); }
}
