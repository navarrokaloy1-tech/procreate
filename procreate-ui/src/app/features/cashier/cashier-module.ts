import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { CashierRoutingModule } from './cashier-routing-module';
import { PatientOrdersComponent } from './pages/patient-orders/patient-orders';
import { IconComponent } from '../../core/components/icon/icon';
import { TestOrderSheetComponent } from '../../core/components/test-order-sheet/test-order-sheet';

@NgModule({
  declarations: [PatientOrdersComponent],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    CashierRoutingModule,
    IconComponent,
    TestOrderSheetComponent
  ]
})
export class CashierModule {}
