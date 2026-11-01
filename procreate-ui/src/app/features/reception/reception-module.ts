import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ReceptionRoutingModule } from './reception-routing-module';
import { ReceptionQueueComponent } from './pages/reception-queue/reception-queue';
import { IconComponent } from '../../core/components/icon/icon';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';

@NgModule({
  declarations: [ReceptionQueueComponent],
  imports: [CommonModule, ReceptionRoutingModule, FormsModule, IconComponent, ScrollOnErrorDirective],
})
export class ReceptionModule {}
