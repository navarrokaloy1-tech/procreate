import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { TestPanelsRoutingModule } from './test-panels-routing-module';
import { TestPanelsComponent } from './pages/test-panels/test-panels';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [TestPanelsComponent],
  imports: [CommonModule, TestPanelsRoutingModule, FormsModule, IconComponent],
})
export class TestPanelsModule {}
