import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { DoctorsRoutingModule } from './doctors-routing-module';
import { DoctorListComponent } from './pages/doctor-list/doctor-list';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [DoctorListComponent],
  imports: [CommonModule, DoctorsRoutingModule, FormsModule, IconComponent],
})
export class DoctorsModule {}
