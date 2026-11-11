import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface EligibleExpense { transactionId: string; transactionVersion: string; accountId: string; accountArchived: boolean; amount: string; description: string; transactionDate: string }
export interface EligibleExpensePage { items: EligibleExpense[]; page: number; pageSize: number; totalCount: number }
export interface SettlementReceipt { id: string; billId: string; transactionId: string; origin: 'linkedExpense' | 'createdExpense'; createdAtUtc: string }
export type SettlementBody =
  | { mode: 'link'; billVersion: string; transactionId: string; transactionVersion: string; accountId: null; paymentDate: null }
  | { mode: 'create'; billVersion: string; transactionId: null; transactionVersion: null; accountId: string; paymentDate: string };

@Injectable({ providedIn: 'root' })
export class BillSettlementApiService {
  private readonly http = inject(HttpClient);
  eligible(billId: string): Promise<EligibleExpensePage> { return firstValueFrom(this.http.get<EligibleExpensePage>(this.base(billId) + '/eligible-expenses', { params: new HttpParams().set('pageSize', 100) })); }
  settle(billId: string, key: string, body: SettlementBody): Promise<SettlementReceipt> { return firstValueFrom(this.http.post<SettlementReceipt>(this.base(billId) + '/settlements', body, { headers: { 'Idempotency-Key': key } })); }
  reverse(billId: string, settlementId: string): Promise<void> { return firstValueFrom(this.http.put<void>(this.base(billId) + '/settlements/' + encodeURIComponent(settlementId) + '/status', { status: 'reversed' })); }
  private base(id: string): string { return '/api/bills/' + encodeURIComponent(id); }
}
