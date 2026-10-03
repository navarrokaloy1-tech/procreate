import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CustomRolesComponent } from './pages/custom-roles/custom-roles';

const routes: Routes = [{ path: '', component: CustomRolesComponent }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class CustomRolesRoutingModule {}
