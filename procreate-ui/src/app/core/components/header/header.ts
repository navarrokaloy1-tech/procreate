import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService, AuthUser } from '../../services/auth';

@Component({
  selector: 'app-header',
  standalone: false,
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class HeaderComponent implements OnInit {
  currentUser$!: Observable<AuthUser | null>;
  menuOpen = false;

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.currentUser$ = this.authService.user$;
  }

  toggleMenu(): void {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  logout(): void {
    this.menuOpen = false;
    this.authService.logout();
  }
}
