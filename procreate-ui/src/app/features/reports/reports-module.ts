import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ReportsRoutingModule } from './reports-routing-module';
import { Reports } from './pages/reports/reports';

@NgModule({
  declarations: [Reports],
  imports: [CommonModule, ReportsRoutingModule, FormsModule],
})
export class ReportsModule {}
