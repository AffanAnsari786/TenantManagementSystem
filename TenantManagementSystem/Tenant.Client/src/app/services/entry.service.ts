import { Inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/tokens/api-tokens';

export interface EntryRecord {
  id: string;
  rentPeriod: string;
  amount: number;
  receivedDate: string;
  createdDate: string;
  tenantSign?: string;
}

export interface Entry {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  address?: string;
  aadhaarNumber?: string;
  propertyName?: string;
  records: EntryRecord[];
}

export interface CreateEntryRequest {
  name: string;
  startDate: string;
  endDate: string;
  address?: string;
  aadhaarNumber?: string;
  propertyName?: string;
}

export interface PagedResponse<T> {
  data: T[];
  totalRecords: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
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
  private readonly apiUrl: string;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.apiUrl = `${apiBaseUrl}/entries`;
  }

  getEntries(page: number = 1, pageSize: number = 10): Observable<PagedResponse<Entry>> {
    return this.http.get<PagedResponse<Entry>>(`${this.apiUrl}?page=${page}&pageSize=${pageSize}`);
  }

  getEntry(id: string): Observable<Entry> {
    return this.http.get<Entry>(`${this.apiUrl}/${id}`);
  }

  createEntry(entry: CreateEntryRequest): Observable<Entry> {
    return this.http.post<Entry>(this.apiUrl, entry);
  }

  updateEntry(id: string, entry: CreateEntryRequest): Observable<Entry> {
    return this.http.put<Entry>(`${this.apiUrl}/${id}`, entry);
  }

  deleteEntry(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  addRecord(entryId: string, record: CreateRecordRequest): Observable<EntryRecord> {
    return this.http.post<EntryRecord>(`${this.apiUrl}/${entryId}/records`, record);
  }

  updateRecord(entryId: string, recordId: string, record: UpdateRecordRequest): Observable<EntryRecord> {
    return this.http.put<EntryRecord>(`${this.apiUrl}/${entryId}/records/${recordId}`, record);
  }

  deleteRecord(entryId: string, recordId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${entryId}/records/${recordId}`);
  }
}
