import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PatientSignup } from './pages/patient-signup/patient-signup';

const routes: Routes = [{ path: '', component: PatientSignup }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PublicRegistrationRoutingModule {}
