import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Reports } from './pages/reports/reports';

const routes: Routes = [{ path: '', component: Reports }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class ReportsRoutingModule {}
