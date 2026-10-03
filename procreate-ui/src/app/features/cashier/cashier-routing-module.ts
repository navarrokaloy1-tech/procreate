import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { VisitForm } from '../visits/pages/visit-form/visit-form';

// The Cashier Desk uses the same batch test-ordering screen as New Consultation
// (a standalone component), so the cashier can register one or many patients at
// once. Routed under /app/cashier, so the breadcrumb stays "Cashier Desk".
const routes: Routes = [
  { path: '', component: VisitForm }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CashierRoutingModule {}
