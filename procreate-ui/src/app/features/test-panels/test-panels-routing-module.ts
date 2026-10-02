import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { TestPanelsComponent } from './pages/test-panels/test-panels';

const routes: Routes = [{ path: '', component: TestPanelsComponent }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class TestPanelsRoutingModule {}
