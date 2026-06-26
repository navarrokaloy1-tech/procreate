import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { BillingList } from './pages/billing-list/billing-list';
import { BillingForm } from './pages/billing-form/billing-form';

const routes: Routes = [
  { path: '', component: BillingList },
  { path: 'new/:visitId', component: BillingForm },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class BillingRoutingModule {}
