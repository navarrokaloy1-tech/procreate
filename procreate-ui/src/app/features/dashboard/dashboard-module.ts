import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../core/components/icon/icon';
import { DashboardRoutingModule } from './dashboard-routing-module';
import { DashboardComponent } from './pages/dashboard/dashboard';

@NgModule({
  declarations: [DashboardComponent],
  imports: [CommonModule, DashboardRoutingModule, IconComponent]
})
export class DashboardModule {}
