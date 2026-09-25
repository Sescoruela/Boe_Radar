import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface SubscriptionPreferences {
  categories: string[];
  keywords: string[];
  digestHour: number;
}

export interface SubscriptionView {
  email: string;
  status: string;
  preferences: SubscriptionPreferences;
}

@Injectable({ providedIn: 'root' })
export class SubscriptionsApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/subscriptions';

  register(email: string, preferences: SubscriptionPreferences, consent: boolean) {
    return this.http.post(this.base, { email, ...preferences, consent });
  }

  verify(token: string) {
    return this.http.post<{ managementToken: string }>(`${this.base}/verify`, { token });
  }

  get(token: string) {
    return this.http.get<SubscriptionView>(`${this.base}/me`, {
      headers: this.headers(token),
    });
  }

  update(token: string, preferences: SubscriptionPreferences) {
    return this.http.put(`${this.base}/me`, preferences, { headers: this.headers(token) });
  }

  unsubscribe(token: string) {
    return this.http.post(`${this.base}/unsubscribe`, { token });
  }

  private headers(token: string): HttpHeaders {
    return new HttpHeaders({ 'X-Management-Token': token });
  }
}
