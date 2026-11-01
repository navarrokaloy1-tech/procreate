import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { LoginActivityRoutingModule } from './login-activity-routing-module';
import { LoginActivityComponent } from './pages/login-activity/login-activity';
import { IconComponent } from '../../core/components/icon/icon';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';

@NgModule({
  declarations: [LoginActivityComponent],
  imports: [CommonModule, LoginActivityRoutingModule, FormsModule, IconComponent, ScrollOnErrorDirective],
})
export class LoginActivityModule {}
