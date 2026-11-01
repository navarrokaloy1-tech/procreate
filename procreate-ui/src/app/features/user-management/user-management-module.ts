import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { UserManagementRoutingModule } from './user-management-routing-module';
import { UserListComponent } from './pages/user-list/user-list';
import { IconComponent } from '../../core/components/icon/icon';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';

@NgModule({
  declarations: [UserListComponent],
  imports: [CommonModule, UserManagementRoutingModule, FormsModule, IconComponent, ScrollOnErrorDirective],
})
export class UserManagementModule {}
