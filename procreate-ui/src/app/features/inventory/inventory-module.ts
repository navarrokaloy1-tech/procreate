import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { InventoryRoutingModule } from './inventory-routing-module';
import { InventoryComponent } from './pages/inventory/inventory';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [InventoryComponent],
  imports: [CommonModule, InventoryRoutingModule, FormsModule, IconComponent],
})
export class InventoryModule {}
