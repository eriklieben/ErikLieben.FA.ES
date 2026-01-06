import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  UserProfileDto,
  UserProfileDtoSchema,
  CreateUserProfileRequest,
  UpdateUserProfileRequest,
  CommandResult,
  CommandResultSchema,
  PaginationInfo,
  PaginationInfoSchema,
  UserProfilePage,
  UserProfilePageSchema,
  TeamMemberDto,
  TeamMemberDtoSchema,
} from '../contracts/user-profile.contracts';
import { z } from 'zod';

@Injectable({
  providedIn: 'root'
})
export class UserProfileApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/userprofiles`;

  /**
   * Get all user profiles
   */
  getAllProfiles(): Observable<UserProfileDto[]> {
    return this.http.get(this.baseUrl).pipe(
      map(response => z.array(UserProfileDtoSchema).parse(response))
    );
  }

  /**
   * Get pagination info (total users, total pages, users per page)
   */
  getPaginationInfo(): Observable<PaginationInfo> {
    return this.http.get(`${this.baseUrl}/paged`).pipe(
      map(response => PaginationInfoSchema.parse(response))
    );
  }

  /**
   * Get a specific page of user profiles
   */
  getPage(pageNumber: number): Observable<UserProfilePage> {
    return this.http.get(`${this.baseUrl}/paged/${pageNumber}`).pipe(
      map(response => UserProfilePageSchema.parse(response))
    );
  }

  /**
   * Get a specific user profile by ID
   */
  getProfile(userId: string): Observable<UserProfileDto> {
    return this.http.get(`${this.baseUrl}/${userId}`).pipe(
      map(response => UserProfileDtoSchema.parse(response))
    );
  }

  /**
   * Create a new user profile
   */
  createProfile(request: CreateUserProfileRequest): Observable<CommandResult> {
    return this.http.post(this.baseUrl, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  /**
   * Update an existing user profile
   */
  updateProfile(userId: string, request: UpdateUserProfileRequest): Observable<CommandResult> {
    return this.http.put(`${this.baseUrl}/${userId}`, request).pipe(
      map(response => CommandResultSchema.parse(response))
    );
  }

  /**
   * Get team members (non-stakeholders) for dropdowns
   */
  getTeamMembers(): Observable<TeamMemberDto[]> {
    return this.http.get(`${this.baseUrl}/team-members`).pipe(
      map(response => z.array(TeamMemberDtoSchema).parse(response))
    );
  }
}
