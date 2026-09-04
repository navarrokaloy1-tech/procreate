import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

import { PatientPortalRoutingModule } from './patient-portal-routing-module';
import { PortalLoginComponent } from './pages/portal-login/portal-login';
import { PortalHomeComponent } from './pages/portal-home/portal-home';
import { IconComponent } from '../../core/components/icon/icon';
import { QrScannerComponent } from '../../core/components/qr-scanner/qr-scanner';

@NgModule({
  declarations: [PortalLoginComponent, PortalHomeComponent],
  imports: [
    CommonModule,
    PatientPortalRoutingModule,
    ReactiveFormsModule,
    IconComponent,
    QrScannerComponent,
  ],
})
export class PatientPortalModule {}
