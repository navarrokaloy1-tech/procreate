import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LabOrders } from './pages/lab-orders/lab-orders';
import { ResultEntry } from './pages/result-entry/result-entry';

const routes: Routes = [
  { path: '', component: LabOrders },
  { path: ':id/encode', component: ResultEntry }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class LabResultsRoutingModule {}
