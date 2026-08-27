import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PatientListComponent } from './pages/patient-list/patient-list';
import { PatientDetailComponent } from './pages/patient-detail/patient-detail';

// Registration and editing happen in a modal on the list, so the old page
// routes redirect there rather than 404ing on stale links/bookmarks. ':id/edit'
// is declared above ':id' so it is not swallowed as a patient id.
const routes: Routes = [
  { path: '', component: PatientListComponent },
  { path: 'new', redirectTo: '', pathMatch: 'full' },
  { path: ':id/edit', redirectTo: ':id' },
  { path: ':id', component: PatientDetailComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PatientsRoutingModule {}
