import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ShareLinkRequest {
  entryId: number;
  expiryDays?: number;
}

export interface ShareLinkResponse {
  shareToken: string;
  shareUrl: string;
  expiryDate: string;
}

export interface SharedEntry {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  records: Array<{
    id: number;
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
  private apiUrl = 'http://localhost:5149/api/share'; // Using HTTP instead of HTTPS for development

  constructor(private http: HttpClient) { }

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
