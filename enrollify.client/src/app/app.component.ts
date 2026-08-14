import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NotificationHostComponent } from './core/components/notification-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NotificationHostComponent],
  template: `
    <router-outlet />
    <app-notification-host />
  `
})
export class AppComponent {}
