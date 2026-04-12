/**
 * Workspace list item model
 */
export interface WorkspaceListItem {
    workspaceId: string;
    name: string;
    role: string;
}

/**
 * Member list item model
 */
export interface MemberListItem {
    userId: string;
    displayName: string;
    email: string;
    role: string;
}

/**
 * Workspace response model
 */
export interface WorkspaceResponse {
    id: string;
    name: string;
    members: MemberListItem[];
}

/**
 * Post workspace request model
 */
export interface PostWorkspaceRequest {
    name: string;
}

/**
 * Post workspace user request model
 */
export interface PostWorkspaceUserRequest {
    userId: string;
}
