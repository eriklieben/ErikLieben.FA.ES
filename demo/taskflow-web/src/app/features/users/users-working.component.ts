import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { UserContextService } from '../../core/services/user-context.service';
import { UserProfileDto } from '../../core/contracts/user-profile.contracts';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule
  ],
  templateUrl: './users-working.component.html',
  styleUrl: './users-working.component.css'
})
export class UsersWorkingComponent {
  readonly userContext = inject(UserContextService);
  readonly displayedColumns = ['displayName', 'email', 'role', 'createdAt'];
  readonly currentUserId = this.userContext.currentUserId;

  isAdmin(user: UserProfileDto): boolean {
    return user.userId === '00000000-0000-0000-0000-000000000001';
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
