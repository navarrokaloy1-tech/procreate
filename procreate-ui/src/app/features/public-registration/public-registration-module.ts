import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { PublicRegistrationRoutingModule } from './public-registration-routing-module';
import { PatientSignup } from './pages/patient-signup/patient-signup';

@NgModule({
  declarations: [PatientSignup],
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PublicRegistrationRoutingModule],
})
export class PublicRegistrationModule {}
