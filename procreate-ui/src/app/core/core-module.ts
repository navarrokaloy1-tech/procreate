import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { LayoutComponent } from './components/layout/layout';
import { SidebarComponent } from './components/sidebar/sidebar';
import { HeaderComponent } from './components/header/header';
import { LoginComponent } from './pages/login/login';
import { IconComponent } from './components/icon/icon';
import { QrScannerComponent } from './components/qr-scanner/qr-scanner';

@NgModule({
  declarations: [
    LayoutComponent,
    SidebarComponent,
    HeaderComponent,
    LoginComponent
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    HttpClientModule,
    IconComponent,
    QrScannerComponent
  ],
  exports: [
    LayoutComponent,
    SidebarComponent,
    HeaderComponent,
    LoginComponent,
    IconComponent
  ]
})
export class CoreModule { }
