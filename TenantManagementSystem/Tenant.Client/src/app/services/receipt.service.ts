import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ReceiptService {
  private apiUrl = 'http://localhost:5149/api/receipts';

  constructor(private http: HttpClient) {}

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
