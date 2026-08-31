import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ServicesRoutingModule } from './services-routing-module';
import { ServiceListComponent } from './pages/service-list/service-list';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [ServiceListComponent],
  imports: [CommonModule, ServicesRoutingModule, FormsModule, IconComponent],
})
export class ServicesModule {}
