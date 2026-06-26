import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { VisitsRoutingModule } from './visits-routing-module';
import { VisitList } from './pages/visit-list/visit-list';
import { VisitForm } from './pages/visit-form/visit-form';

@NgModule({
  declarations: [VisitList, VisitForm],
  imports: [CommonModule, VisitsRoutingModule, ReactiveFormsModule, FormsModule],
})
export class VisitsModule {}
