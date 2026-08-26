import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { VisitsRoutingModule } from './visits-routing-module';
import { VisitList } from './pages/visit-list/visit-list';
import { VisitForm } from './pages/visit-form/visit-form';
import { VisitDetail } from './pages/visit-detail/visit-detail';
import { IconComponent } from '../../core/components/icon/icon';

@NgModule({
  declarations: [VisitList, VisitForm, VisitDetail],
  imports: [
    CommonModule,
    VisitsRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    IconComponent,
  ],
})
export class VisitsModule {}
