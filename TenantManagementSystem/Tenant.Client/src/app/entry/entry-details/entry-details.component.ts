import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';

@Component({
  selector: 'app-entry-details',
  imports: [CommonModule, MatCardModule, MatButtonModule, MatTableModule],
  templateUrl: './entry-details.component.html',
  styleUrl: './entry-details.component.scss'
})
export class EntryDetailsComponent implements OnInit {
  entry: any = {}; // Replace with Entry model later
  displayedColumns: string[] = ['month', 'date', 'money', 'remainingMoney', 'ownerSign', 'tenantSign'];

  constructor(private route: ActivatedRoute) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    // For now, use static data
    this.entry = {
      id: id,
      name: 'Entry ' + id,
      startDate: new Date(),
      endDate: new Date(),
      records: [
        { month: 'January', date: new Date(), money: 1000, remainingMoney: 500, ownerSign: 'Owner', tenantSign: '' },
        { month: 'February', date: new Date(), money: 1000, remainingMoney: 0, ownerSign: 'Owner', tenantSign: 'Tenant' }
      ]
    };
  }

  onAddRecord() {
    // Open modal to add record
    console.log('Add new record');
  }

  onUpdateTenantSign(record: any) {
    // Open modal to update tenant sign
    console.log('Update tenant sign for record', record);
  }
} 
