import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { UserManagementRoutingModule } from './user-management-routing-module';
import { UserListComponent } from './pages/user-list/user-list';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [UserListComponent],
  imports: [CommonModule, UserManagementRoutingModule, FormsModule, IconComponent],
})
export class UserManagementModule {}
