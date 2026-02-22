import { z } from 'zod';

// Admin user constant - matches backend
export const ADMIN_USER_ID = '00000000-0000-0000-0000-000000000001';

/**
 * UserProfile DTO from API
 */
export const UserProfileDtoSchema = z.object({
  userId: z.string(),
  displayName: z.string(),
  email: z.string().email(),
  jobRole: z.string(),
  createdAt: z.string(),
  lastUpdatedAt: z.string().nullable(),
});

export type UserProfileDto = z.infer<typeof UserProfileDtoSchema>;

/**
 * Pagination info for user profiles
 */
export const PaginationInfoSchema = z.object({
  totalUsers: z.number(),
  totalPages: z.number(),
  usersPerPage: z.number(),
});

export type PaginationInfo = z.infer<typeof PaginationInfoSchema>;

/**
 * A page of user profiles
 */
export const UserProfilePageSchema = z.object({
  pageNumber: z.number(),
  totalPages: z.number(),
  totalUsers: z.number(),
  users: z.array(UserProfileDtoSchema),
});

export type UserProfilePage = z.infer<typeof UserProfilePageSchema>;

/**
 * Request to create a new user profile
 */
export interface CreateUserProfileRequest {
  userId: string;
  displayName: string;
  email: string;
}

/**
 * Request to update a user profile
 */
export interface UpdateUserProfileRequest {
  displayName: string;
  email: string;
}

/**
 * Command result
 */
export const CommandResultSchema = z.object({
  success: z.boolean(),
  message: z.string().optional(),
});

export type CommandResult = z.infer<typeof CommandResultSchema>;

/**
 * Team member DTO (for dropdowns - non-stakeholders only)
 */
export const TeamMemberDtoSchema = z.object({
  userId: z.string(),
  displayName: z.string(),
  email: z.string().email(),
});

export type TeamMemberDto = z.infer<typeof TeamMemberDtoSchema>;
