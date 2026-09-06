import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MedicalRecordsRoutingModule } from './medical-records-routing-module';
import { CertificateListComponent } from './pages/certificate-list/certificate-list';
import { IconComponent } from '../../core/components/icon/icon';
import { ComboSelectComponent } from '../../core/components/combo-select/combo-select';
import { CertificateFormComponent } from '../../core/components/certificate-form/certificate-form';

@NgModule({
  declarations: [CertificateListComponent],
  imports: [
    CommonModule,
    MedicalRecordsRoutingModule,
    FormsModule,
    IconComponent,
    ComboSelectComponent,
    CertificateFormComponent,
  ],
})
export class MedicalRecordsModule {}
