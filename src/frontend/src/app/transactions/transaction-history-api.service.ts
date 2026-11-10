import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Transaction, TransactionKind } from './transaction-command-api.service';

export interface TransactionFilters {
  accountId?: string;
  kind?: TransactionKind;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}
export interface TransactionHistory {
  items: Transaction[];
  page: number;
  pageSize: number;
  totalCount: number;
  from: string;
  to: string;
  totals: { income: string; expense: string; net: string };
}

@Injectable({ providedIn: 'root' })
export class TransactionHistoryApiService {
  private readonly http = inject(HttpClient);
  query(filters: TransactionFilters): Promise<TransactionHistory> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.accountId) params = params.set('accountId', filters.accountId);
    if (filters.kind) params = params.set('kind', filters.kind);
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    return firstValueFrom(this.http.get<TransactionHistory>('/api/transactions', { params }));
  }
}
