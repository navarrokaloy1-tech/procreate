import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { PatientsRoutingModule } from './patients-routing-module';
import { PatientListComponent } from './pages/patient-list/patient-list';
import { PatientFormComponent } from './pages/patient-form/patient-form';
import { PatientDetailComponent } from './pages/patient-detail/patient-detail';
import { SoapNotePageComponent } from './pages/soap-note/soap-note';
import { IconComponent } from '../../core/components/icon/icon';
import { ComboSelectComponent } from '../../core/components/combo-select/combo-select';
import { CertificateFormComponent } from '../../core/components/certificate-form/certificate-form';
import { ResultDeliveryComponent } from '../../core/components/result-delivery/result-delivery';
import { TestOrderSheetComponent } from '../../core/components/test-order-sheet/test-order-sheet';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';
import { PatientSummaryComponent } from '../../core/components/patient-summary/patient-summary';

@NgModule({
  declarations: [
    PatientListComponent,
    PatientFormComponent,
    PatientDetailComponent,
    SoapNotePageComponent,
  ],
  imports: [
    CommonModule,
    PatientsRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    IconComponent,
    ComboSelectComponent,
    CertificateFormComponent,
    ResultDeliveryComponent,
    TestOrderSheetComponent,
    ScrollOnErrorDirective,
    PatientSummaryComponent,
  ],
})
export class PatientsModule {}
