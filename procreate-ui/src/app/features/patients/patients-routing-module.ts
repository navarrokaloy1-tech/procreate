import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PatientListComponent } from './pages/patient-list/patient-list';

// Registration and editing now happen in a modal on the list, so the old
// page routes redirect there rather than 404ing on stale links/bookmarks.
const routes: Routes = [
  { path: '', component: PatientListComponent },
  { path: 'new', redirectTo: '', pathMatch: 'full' },
  { path: ':id/edit', redirectTo: '', pathMatch: 'full' },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PatientsRoutingModule {}
