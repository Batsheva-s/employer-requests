import { Component, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';
import { CurrentUserService } from './core/services/current-user.service';
import { RequestsPageComponent } from './features/requests/requests-page/requests-page.component';

@Component({
  selector: 'app-root',
  imports: [MatToolbarModule, MatFormFieldModule, MatInputModule, RequestsPageComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {
  protected readonly currentUser = inject(CurrentUserService);
}
