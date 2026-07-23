import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PatientOrdersComponent } from './pages/patient-orders/patient-orders';

const routes: Routes = [
  { path: '', component: PatientOrdersComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CashierRoutingModule {}
