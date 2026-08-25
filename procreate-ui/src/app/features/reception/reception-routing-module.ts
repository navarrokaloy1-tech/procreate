import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReceptionQueueComponent } from './pages/reception-queue/reception-queue';

const routes: Routes = [{ path: '', component: ReceptionQueueComponent }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class ReceptionRoutingModule {}
