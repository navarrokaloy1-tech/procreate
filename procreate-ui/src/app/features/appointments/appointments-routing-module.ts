import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AppointmentCalendarComponent } from './pages/appointment-calendar/appointment-calendar';

const routes: Routes = [{ path: '', component: AppointmentCalendarComponent }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class AppointmentsRoutingModule {}
