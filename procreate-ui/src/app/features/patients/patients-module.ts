import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { PatientsRoutingModule } from './patients-routing-module';
import { PatientListComponent } from './pages/patient-list/patient-list';
import { PatientFormComponent } from './pages/patient-form/patient-form';

@NgModule({
  declarations: [PatientListComponent, PatientFormComponent],
  imports: [
    CommonModule,
    PatientsRoutingModule,
    ReactiveFormsModule,
    FormsModule,
  ],
})
export class PatientsModule {}
