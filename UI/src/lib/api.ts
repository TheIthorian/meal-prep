import { PostWorkspaceRequest, WorkspaceResponse } from '@/models/workspace';
import { httpClient } from './http-client';
import { LoginRequest, RegisterRequest } from '@/models/auth';
import { UserResponse } from '@/models/user';

// Auth API
export const authApi = {
    login: (data: LoginRequest) => httpClient.post<void>('/api/v1/auth/login?useSessionCookies=true', data),
    register: (data: RegisterRequest) => httpClient.post<void>('/api/v1/auth/signup', data),
    logout: () => httpClient.post<void>('/api/v1/auth/logout', {}),
    getMe: () => httpClient.get<UserResponse>('/api/v1/me'),
    updateMe: (data: { displayName: string }) => httpClient.patch<UserResponse>('/api/v1/me', data),
    deleteMe: () => httpClient.delete<void>('/api/v1/me'),
};

// Workspaces API
export const workspacesApi = {
    getAll: () => httpClient.get<WorkspaceResponse[]>('/api/v1/workspaces'),
    getById: (id: string) => httpClient.get<WorkspaceResponse>(`/api/v1/workspaces/${id}`),
    create: (data: PostWorkspaceRequest) => httpClient.post<WorkspaceResponse>('/api/v1/workspaces', data),
    update: (id: string, data: Partial<PostWorkspaceRequest>) =>
        httpClient.patch<WorkspaceResponse>(`/api/v1/workspaces/${id}`, data),
    delete: (id: string) => httpClient.delete<void>(`/api/v1/workspaces/${id}`),
    addMember: (workspaceId: string, email: string, role: string) =>
        httpClient.post<void>(`/api/v1/workspaces/${workspaceId}/members`, { email, role }),
    updateMemberRole: (workspaceId: string, userId: string, role: string) =>
        httpClient.patch<void>(`/api/v1/workspaces/${workspaceId}/members/${userId}`, { role }),
    removeMember: (workspaceId: string, userId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/members/${userId}`),
};
