import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { PublicRegistrationRoutingModule } from './public-registration-routing-module';
import { PatientSignup } from './pages/patient-signup/patient-signup';
import { IconComponent } from '../../core/components/icon/icon';
import { ComboSelectComponent } from '../../core/components/combo-select/combo-select';
import { ScrollOnErrorDirective } from '../../core/directives/scroll-on-error.directive';

@NgModule({
  declarations: [PatientSignup],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    PublicRegistrationRoutingModule,
    IconComponent,
    ComboSelectComponent,
    ScrollOnErrorDirective,
  ],
})
export class PublicRegistrationModule {}
