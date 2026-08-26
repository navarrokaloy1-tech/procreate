import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { LabResultsRoutingModule } from './lab-results-routing-module';
import { LabOrders } from './pages/lab-orders/lab-orders';
import { ResultEntry } from './pages/result-entry/result-entry';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [LabOrders, ResultEntry],
  imports: [
    CommonModule,
    LabResultsRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    IconComponent,
  ],
})
export class LabResultsModule {}
