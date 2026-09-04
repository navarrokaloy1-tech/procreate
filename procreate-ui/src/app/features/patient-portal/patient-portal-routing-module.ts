import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PortalLoginComponent } from './pages/portal-login/portal-login';
import { PortalHomeComponent } from './pages/portal-home/portal-home';
import { PatientGuard } from '../../core/guards/patient-guard';

// 'login' sits above '' so the sign-in page stays reachable without a session.
const routes: Routes = [
  { path: 'login', component: PortalLoginComponent },
  { path: '', component: PortalHomeComponent, canActivate: [PatientGuard] },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PatientPortalRoutingModule {}
