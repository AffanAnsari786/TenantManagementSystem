import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface EntryRecord {
  id: number;
  rentPeriod: string;
  amount: number;
  receivedDate: string;
  createdDate: string;
  tenantSign?: string;
}

export interface Entry {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  records: EntryRecord[];
}

export interface CreateEntryRequest {
  name: string;
  startDate: string;
  endDate: string;
}

export interface CreateRecordRequest {
  rentPeriod: string;
  amount: number;
  receivedDate: string;
}

export interface UpdateRecordRequest {
  rentPeriod: string;
  amount: number;
  receivedDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class EntryService {
  private apiUrl = 'http://localhost:5149/api/entries'; // Using HTTP instead of HTTPS for development

  constructor(private http: HttpClient) { }

  getEntries(): Observable<Entry[]> {
    return this.http.get<Entry[]>(this.apiUrl);
  }

  getEntry(id: number): Observable<Entry> {
    return this.http.get<Entry>(`${this.apiUrl}/${id}`);
  }

  createEntry(entry: CreateEntryRequest): Observable<Entry> {
    return this.http.post<Entry>(this.apiUrl, entry);
  }

  addRecord(entryId: number, record: CreateRecordRequest): Observable<EntryRecord> {
    return this.http.post<EntryRecord>(`${this.apiUrl}/${entryId}/records`, record);
  }

  updateRecord(entryId: number, recordId: number, record: UpdateRecordRequest): Observable<EntryRecord> {
    return this.http.put<EntryRecord>(`${this.apiUrl}/${entryId}/records/${recordId}`, record);
  }

  deleteRecord(entryId: number, recordId: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${entryId}/records/${recordId}`);
  }
}
