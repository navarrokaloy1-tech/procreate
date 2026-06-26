import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { VisitList } from './pages/visit-list/visit-list';
import { VisitForm } from './pages/visit-form/visit-form';

const routes: Routes = [
  { path: '', component: VisitList },
  { path: 'new', component: VisitForm },
  { path: ':id', component: VisitForm },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class VisitsRoutingModule {}
