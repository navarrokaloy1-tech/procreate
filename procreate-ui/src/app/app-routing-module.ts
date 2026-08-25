import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './core/pages/login/login';
import { LayoutComponent } from './core/components/layout/layout';
import { AuthGuard } from './core/guards/auth-guard';

const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'register',
    loadChildren: () =>
      import('./features/public-registration/public-registration-module').then(
        m => m.PublicRegistrationModule
      )
  },
  {
    path: 'app',
    component: LayoutComponent,
    canActivate: [AuthGuard],
    children: [
      {
        path: 'cashier',
        loadChildren: () =>
          import('./features/cashier/cashier-module').then(m => m.CashierModule)
      },
      {
        path: 'orders',
        loadChildren: () =>
          import('./features/orders/orders-module').then(m => m.OrdersModule)
      },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard-module').then(m => m.DashboardModule)
      },
      {
        path: 'appointments',
        loadChildren: () =>
          import('./features/appointments/appointments-module').then(m => m.AppointmentsModule)
      },
      {
        path: 'reception-queue',
        loadChildren: () =>
          import('./features/reception/reception-module').then(m => m.ReceptionModule)
      },
      {
        path: 'doctors',
        loadChildren: () =>
          import('./features/doctors/doctors-module').then(m => m.DoctorsModule)
      },
      {
        path: 'patients',
        loadChildren: () =>
          import('./features/patients/patients-module').then(m => m.PatientsModule)
      },
      {
        path: 'visits',
        loadChildren: () =>
          import('./features/visits/visits-module').then(m => m.VisitsModule)
      },
      {
        path: 'lab-results',
        loadChildren: () =>
          import('./features/lab-results/lab-results-module').then(m => m.LabResultsModule)
      },
      {
        path: 'billing',
        loadChildren: () =>
          import('./features/billing/billing-module').then(m => m.BillingModule)
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('./features/reports/reports-module').then(m => m.ReportsModule)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: '',
    redirectTo: '/app/dashboard',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: '/app/dashboard'
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
