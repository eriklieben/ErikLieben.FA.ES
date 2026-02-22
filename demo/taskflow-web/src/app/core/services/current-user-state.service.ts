import { Injectable, signal } from '@angular/core';
import { ADMIN_USER_ID } from '../contracts/user-profile.contracts';

const CURRENT_USER_KEY = 'taskflow_current_user_id';

/**
 * Simple state service that manages the current user ID.
 * This service has NO dependencies to avoid circular dependency issues with HTTP interceptor.
 */
@Injectable({
  providedIn: 'root'
})
export class CurrentUserStateService {
  // Currently selected user ID
  private readonly _currentUserId = signal<string>(this.loadSavedUserId());
  readonly currentUserId = this._currentUserId.asReadonly();

  /**
   * Switch to a different user
   */
  switchUser(userId: string): void {
    this._currentUserId.set(userId);
    localStorage.setItem(CURRENT_USER_KEY, userId);
  }

  /**
   * Load the saved user ID from localStorage, defaulting to admin
   */
  private loadSavedUserId(): string {
    return localStorage.getItem(CURRENT_USER_KEY) || ADMIN_USER_ID;
  }

  /**
   * Clear the current user and reset to admin
   */
  reset(): void {
    this._currentUserId.set(ADMIN_USER_ID);
    localStorage.removeItem(CURRENT_USER_KEY);
  }
}
