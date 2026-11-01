import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ServicesRoutingModule } from './services-routing-module';
import { ServiceListComponent } from './pages/service-list/service-list';
import { IconComponent } from '../../core/components/icon/icon';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';

@NgModule({
  declarations: [ServiceListComponent],
  imports: [CommonModule, ServicesRoutingModule, FormsModule, IconComponent, ScrollOnErrorDirective],
})
export class ServicesModule {}
