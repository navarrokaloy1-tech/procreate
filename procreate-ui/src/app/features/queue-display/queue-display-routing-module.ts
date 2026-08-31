import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { QueueBoardComponent } from './pages/queue-board/queue-board';

const routes: Routes = [{ path: '', component: QueueBoardComponent }];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class QueueDisplayRoutingModule {}
