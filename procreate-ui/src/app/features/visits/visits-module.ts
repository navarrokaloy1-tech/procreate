import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { VisitsRoutingModule } from './visits-routing-module';
import { VisitList } from './pages/visit-list/visit-list';
import { VisitForm } from './pages/visit-form/visit-form';
import { VisitDetail } from './pages/visit-detail/visit-detail';
import { IconComponent } from '../../core/components/icon/icon';
import { QrScannerComponent } from '../../core/components/qr-scanner/qr-scanner';

@NgModule({
  // VisitForm is standalone (reused by the Cashier Desk), so it is imported
  // rather than declared.
  declarations: [VisitList, VisitDetail],
  imports: [
    CommonModule,
    VisitsRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    IconComponent,
    QrScannerComponent,
    VisitForm,
  ],
})
export class VisitsModule {}

