import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { HomeComponent } from './components/home/home.component';
import { AllTenantsComponent } from './components/all-tenants/all-tenants.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { SharedDashboardComponent } from './shared/shared-dashboard.component';
import { PageNotFoundComponent } from './components/page-not-found/page-not-found.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  // Public, read-only share view — no guard.
  { path: 'shared/:token', component: SharedDashboardComponent },

  // Protected routes — require a valid token.
  { path: 'home', component: HomeComponent, canActivate: [authGuard] },
  { path: 'all-tenants', component: AllTenantsComponent, canActivate: [authGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'dashboard/:entryId', component: DashboardComponent, canActivate: [authGuard] },

  { path: '404', component: PageNotFoundComponent },
  { path: '**', component: PageNotFoundComponent }
];
