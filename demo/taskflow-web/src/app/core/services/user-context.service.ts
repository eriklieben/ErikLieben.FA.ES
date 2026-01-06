import { Injectable, inject, signal, computed } from '@angular/core';
import { UserProfileDto, TeamMemberDto, ADMIN_USER_ID } from '../contracts/user-profile.contracts';
import { UserProfileApiService } from './user-profile-api.service';
import { CurrentUserStateService } from './current-user-state.service';

/**
 * Service that manages the current user context for the application.
 * In demo mode, this allows switching between different user profiles.
 * In production, this would be replaced with actual authentication.
 */
@Injectable({
  providedIn: 'root'
})
export class UserContextService {
  private readonly userProfileApi = inject(UserProfileApiService);
  private readonly currentUserState = inject(CurrentUserStateService);

  // All available user profiles (used for full profile lookups)
  private readonly _allProfiles = signal<UserProfileDto[]>([]);
  readonly allProfiles = this._allProfiles.asReadonly();

  // Team members only (non-stakeholders) - loaded from dedicated endpoint
  private readonly _teamMembers = signal<TeamMemberDto[]>([]);
  readonly teamMembers = this._teamMembers.asReadonly();

  // Expose current user ID from state service
  readonly currentUserId = this.currentUserState.currentUserId;

  // Current user profile (computed from currentUserId and teamMembers for display name)
  readonly currentUser = computed(() => {
    const userId = this.currentUserId();
    const members = this._teamMembers();
    const member = members.find(m => m.userId === userId);
    if (member) {
      return { userId: member.userId, displayName: member.displayName, email: member.email };
    }
    return null;
  });

  // Loading state
  private readonly _isLoading = signal<boolean>(true);
  readonly isLoading = this._isLoading.asReadonly();

  constructor() {
    // Load team members on initialization (fast, small payload)
    this.loadTeamMembers();
  }

  /**
   * Load team members from the dedicated API endpoint
   */
  loadTeamMembers(): void {
    this._isLoading.set(true);
    this.userProfileApi.getTeamMembers().subscribe({
      next: (members) => {
        this._teamMembers.set(members);

        // If current user is not in the list, default to admin
        const currentId = this.currentUserId();
        if (!members.some(m => m.userId === currentId)) {
          this.currentUserState.switchUser(ADMIN_USER_ID);
        }

        this._isLoading.set(false);
      },
      error: (error) => {
        console.error('Failed to load team members:', error);
        this._isLoading.set(false);

        // Default to admin user on error
        this.currentUserState.switchUser(ADMIN_USER_ID);
      }
    });
  }

  /**
   * Load all user profiles from the API (for full profile data)
   * @deprecated Use loadTeamMembers() for dropdown population
   */
  loadProfiles(): void {
    this.userProfileApi.getAllProfiles().subscribe({
      next: (profiles) => {
        this._allProfiles.set(profiles);
      },
      error: (error) => {
        console.error('Failed to load user profiles:', error);
      }
    });
  }

  /**
   * Switch to a different user
   */
  switchUser(userId: string): void {
    this.currentUserState.switchUser(userId);
  }

  /**
   * Clear the current user and reset to admin
   */
  reset(): void {
    this.currentUserState.reset();
  }
}
