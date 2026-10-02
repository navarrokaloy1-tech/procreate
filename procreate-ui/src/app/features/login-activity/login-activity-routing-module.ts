import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginActivityComponent } from './pages/login-activity/login-activity';

const routes: Routes = [{ path: '', component: LoginActivityComponent }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class LoginActivityRoutingModule {}
