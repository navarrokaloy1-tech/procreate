import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { VisitList } from './pages/visit-list/visit-list';
import { VisitForm } from './pages/visit-form/visit-form';
import { VisitDetail } from './pages/visit-detail/visit-detail';

const routes: Routes = [
  { path: '', component: VisitList },
  // 'new' must stay above ':id', or it would be matched as an id.
  { path: 'new', component: VisitForm },
  { path: ':id', component: VisitDetail },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class VisitsRoutingModule {}
