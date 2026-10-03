import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { CustomRolesRoutingModule } from './custom-roles-routing-module';
import { CustomRolesComponent } from './pages/custom-roles/custom-roles';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [CustomRolesComponent],
  imports: [CommonModule, CustomRolesRoutingModule, FormsModule, IconComponent],
})
export class CustomRolesModule {}
