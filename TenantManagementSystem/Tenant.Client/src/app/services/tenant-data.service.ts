import { Injectable } from '@angular/core';

export interface TenantData {
  name: string;
  startDate: string;
  endDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class TenantDataService {
  private tenantData: TenantData | null = null;

  setTenantData(data: TenantData): void {
    this.tenantData = data;
    console.log('TenantDataService - Setting tenant data:', data);
  }

  getTenantData(): TenantData | null {
    console.log('TenantDataService - Getting tenant data:', this.tenantData);
    return this.tenantData;
  }

  clearTenantData(): void {
    this.tenantData = null;
    console.log('TenantDataService - Cleared tenant data');
  }
}
