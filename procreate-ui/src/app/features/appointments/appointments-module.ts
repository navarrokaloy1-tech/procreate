import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DragDropModule } from '@angular/cdk/drag-drop';

import { AppointmentsRoutingModule } from './appointments-routing-module';
import { AppointmentCalendarComponent } from './pages/appointment-calendar/appointment-calendar';
import { AppointmentBatchesComponent } from './pages/appointment-batches/appointment-batches';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [AppointmentCalendarComponent, AppointmentBatchesComponent],
  imports: [
    CommonModule,
    AppointmentsRoutingModule,
    FormsModule,
    DragDropModule,
    IconComponent,
  ],
})
export class AppointmentsModule {}
