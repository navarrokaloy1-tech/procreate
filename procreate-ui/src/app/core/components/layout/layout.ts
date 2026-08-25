import { Component } from '@angular/core';

@Component({
  selector: 'app-layout',
  standalone: false,
  templateUrl: './layout.html',
  styleUrls: ['./layout.scss']
})
export class LayoutComponent {
  readonly year = new Date().getFullYear();
}
