import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { BillingRoutingModule } from './billing-routing-module';
import { BillingList } from './pages/billing-list/billing-list';
import { BillingForm } from './pages/billing-form/billing-form';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';

@NgModule({
  declarations: [BillingList, BillingForm],
  imports: [CommonModule, BillingRoutingModule, ReactiveFormsModule, FormsModule, ScrollOnErrorDirective],
})
export class BillingModule {}
