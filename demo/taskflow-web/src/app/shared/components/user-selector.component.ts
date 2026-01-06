import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { UserContextService } from '../../core/services/user-context.service';

/**
 * User selector dropdown component for demo mode.
 * Allows switching between different user profiles.
 */
@Component({
  selector: 'app-user-selector',
  standalone: true,
  imports: [
    CommonModule,
    MatSelectModule,
    MatFormFieldModule,
    MatIconModule
  ],
  templateUrl: './user-selector.component.html',
  styleUrl: './user-selector.component.css'
})
export class UserSelectorComponent {
  readonly userContext = inject(UserContextService);

  onUserChange(userId: string): void {
    this.userContext.switchUser(userId);
  }
}
