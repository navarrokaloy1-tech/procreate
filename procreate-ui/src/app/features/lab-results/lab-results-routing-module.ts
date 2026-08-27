import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LabOrders } from './pages/lab-orders/lab-orders';
import { ResultEntry } from './pages/result-entry/result-entry';

const routes: Routes = [
  { path: '', component: LabOrders },
  // The encode screen grew into the full order view, so it lives at the order
  // URL now. The old path is kept so existing links still land somewhere.
  { path: ':id/encode', redirectTo: ':id' },
  { path: ':id', component: ResultEntry },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class LabResultsRoutingModule {}
