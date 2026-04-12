import { WorkspaceListItem } from './workspace';

/**
 * User response model
 */
export interface UserResponse {
    userId: string;
    displayName: string;
    email: string;
    workspaces: WorkspaceListItem[];
}

/**
 * Info request model
 */
export interface InfoRequest {
    newEmail?: string | null;
    newPassword?: string | null;
    oldPassword?: string | null;
}

/**
 * Info response model
 */
export interface InfoResponse {
    email?: string | null;
    isEmailConfirmed: boolean;
}
