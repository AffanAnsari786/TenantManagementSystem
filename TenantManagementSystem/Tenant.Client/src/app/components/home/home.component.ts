import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { TenantDataService } from '../../services/tenant-data.service';
import { Router } from '@angular/router';
import { InitialEntryFormComponent } from '../initial-entry-form/initial-entry-form.component';

@Component({
  selector: 'app-home',
  imports: [CommonModule, MatButtonModule, MatIconModule, MatDialogModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  constructor(
    private dialog: MatDialog, 
    private router: Router,
    private tenantDataService: TenantDataService
  ) {}

  openEntryForm() {
    const dialogRef = this.dialog.open(InitialEntryFormComponent, {
      width: '600px',
      disableClose: true,
      panelClass: 'entry-form-dialog'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result?.action === 'create' && result?.data) {
        console.log('Home component - Form result:', result);
        console.log('Home component - Entry data:', result.data);
        
        // Store data in service and also use navigation state as backup
        this.tenantDataService.setTenantData(result.data);
        
        // Navigate to dashboard with the new entry data
        this.router.navigate(['/dashboard'], { state: { newEntry: result.data } });
      }
    });
  }
} 
