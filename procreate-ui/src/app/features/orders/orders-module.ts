import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { OrdersRoutingModule } from './orders-routing-module';
import { OrderListComponent } from './pages/order-list/order-list';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [OrderListComponent],
  imports: [
    CommonModule,
    FormsModule,
    OrdersRoutingModule,
    IconComponent
  ]
})
export class OrdersModule {}
