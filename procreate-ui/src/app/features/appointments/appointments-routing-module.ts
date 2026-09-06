import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AppointmentCalendarComponent } from './pages/appointment-calendar/appointment-calendar';
import { AppointmentBatchesComponent } from './pages/appointment-batches/appointment-batches';

const routes: Routes = [
  { path: '', component: AppointmentCalendarComponent },
  { path: 'batches', component: AppointmentBatchesComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class AppointmentsRoutingModule {}
