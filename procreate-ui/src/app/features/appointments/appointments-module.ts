import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AppointmentsRoutingModule } from './appointments-routing-module';
import { AppointmentCalendarComponent } from './pages/appointment-calendar/appointment-calendar';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [AppointmentCalendarComponent],
  imports: [CommonModule, AppointmentsRoutingModule, FormsModule, IconComponent],
})
export class AppointmentsModule {}
