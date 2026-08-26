import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CertificateListComponent } from './pages/certificate-list/certificate-list';

const routes: Routes = [
  { path: '', redirectTo: 'certificates', pathMatch: 'full' },
  { path: 'certificates', component: CertificateListComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MedicalRecordsRoutingModule {}
