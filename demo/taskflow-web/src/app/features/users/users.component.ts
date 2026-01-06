import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { UserContextService } from '../../core/services/user-context.service';
import { UserProfileApiService } from '../../core/services/user-profile-api.service';
import { UserProfileDto, UserProfilePage } from '../../core/contracts/user-profile.contracts';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatButtonModule,
    MatPaginatorModule
  ],
  templateUrl: './users.component.html',
  styleUrl: './users.component.css'
})
export class UsersComponent implements OnInit {
  readonly userContext = inject(UserContextService);
  private readonly userProfileApi = inject(UserProfileApiService);

  readonly displayedColumns = ['displayName', 'email', 'role', 'createdAt'];

  readonly currentUserId = this.userContext.currentUserId;

  // Pagination state
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly users = signal<UserProfileDto[]>([]);
  readonly currentPage = signal(1);
  readonly totalPages = signal(0);
  readonly totalUsers = signal(0);

  ngOnInit(): void {
    this.loadPage(1);
  }

  loadPage(pageNumber: number): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.userProfileApi.getPage(pageNumber).subscribe({
      next: (page) => {
        this.users.set(page.users);
        this.currentPage.set(page.pageNumber);
        this.totalPages.set(page.totalPages);
        this.totalUsers.set(page.totalUsers);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load user profiles page:', err);
        this.error.set('Failed to load user profiles. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  onPageChange(event: PageEvent): void {
    // PageEvent.pageIndex is 0-based, our API is 1-based
    this.loadPage(event.pageIndex + 1);
  }

  isAdmin(user: UserProfileDto): boolean {
    return user.email === 'admin@taskflow.demo';
  }

  isStakeholder(user: UserProfileDto): boolean {
    return user.email.endsWith('@stakeholders.com');
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
