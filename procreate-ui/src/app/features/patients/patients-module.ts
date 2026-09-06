import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { PatientsRoutingModule } from './patients-routing-module';
import { PatientListComponent } from './pages/patient-list/patient-list';
import { PatientFormComponent } from './pages/patient-form/patient-form';
import { PatientDetailComponent } from './pages/patient-detail/patient-detail';
import { IconComponent } from '../../core/components/icon/icon';
import { ComboSelectComponent } from '../../core/components/combo-select/combo-select';
import { CertificateFormComponent } from '../../core/components/certificate-form/certificate-form';
import { ResultDeliveryComponent } from '../../core/components/result-delivery/result-delivery';

@NgModule({
  declarations: [PatientListComponent, PatientFormComponent, PatientDetailComponent],
  imports: [
    CommonModule,
    PatientsRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    IconComponent,
    ComboSelectComponent,
    CertificateFormComponent,
    ResultDeliveryComponent,
  ],
})
export class PatientsModule {}
