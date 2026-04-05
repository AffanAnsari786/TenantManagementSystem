import { Inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/tokens/api-tokens';

@Injectable({
  providedIn: 'root'
})
export class ReceiptService {
  private readonly apiUrl: string;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.apiUrl = `${apiBaseUrl}/receipts`;
  }

  downloadReceipt(recordId: string | number): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${recordId}`, {
      responseType: 'blob'
    });
  }

  downloadSharedReceipt(recordId: string | number, publicId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/shared/${recordId}?publicId=${publicId}`, {
      responseType: 'blob'
    });
  }
}
