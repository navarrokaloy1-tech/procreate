import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { CashierRoutingModule } from './cashier-routing-module';
import { PatientOrdersComponent } from './pages/patient-orders/patient-orders';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [PatientOrdersComponent],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    CashierRoutingModule,
    IconComponent
  ]
})
export class CashierModule {}
