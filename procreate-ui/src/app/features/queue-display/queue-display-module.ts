import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { QueueDisplayRoutingModule } from './queue-display-routing-module';
import { QueueBoardComponent } from './pages/queue-board/queue-board';

@NgModule({
  declarations: [QueueBoardComponent],
  imports: [CommonModule, QueueDisplayRoutingModule],
})
export class QueueDisplayModule {}
