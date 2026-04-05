import { Inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/tokens/api-tokens';

export interface ShareLinkRequest {
  entryId: string;
  expiryDays?: number;
}

export interface ShareLinkResponse {
  shareToken: string;
  shareUrl: string;
  expiryDate: string;
}

export interface SharedEntry {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  records: Array<{
    id: string;
    rentPeriod: string;
    amount: number;
    receivedDate: string;
    createdDate: string;
    tenantSign?: string;
  }>;
}

@Injectable({
  providedIn: 'root'
})
export class ShareService {
  private readonly apiUrl: string;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.apiUrl = `${apiBaseUrl}/share`;
  }

  generateShareLink(request: ShareLinkRequest): Observable<ShareLinkResponse> {
    return this.http.post<ShareLinkResponse>(`${this.apiUrl}/generate`, request);
  }

  getSharedDashboard(token: string): Observable<SharedEntry> {
    return this.http.get<SharedEntry>(`${this.apiUrl}/${token}`);
  }

  revokeShareLink(token: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${token}`);
  }
}
