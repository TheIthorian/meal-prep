import { PostWorkspaceRequest, WorkspaceResponse } from '@/models/workspace';
import { httpClient } from './http-client';
import { LoginRequest, RegisterRequest } from '@/models/auth';
import { UserResponse } from '@/models/user';
import {
    GenerateShoppingListRequest,
    MealPlanEntry,
    Recipe,
    RecipeImportPreview,
    RecipeListItem,
    SaveMealPlanEntryRequest,
    SaveRecipeRequest,
    SaveShoppingListItemRequest,
    SaveShoppingListRequest,
    ShoppingList,
    ShoppingListItem,
    ShoppingListListItem,
} from '@/models/meal-prep';
import type { PaginatedResponse } from '@/models/pagination';
import type { McpAccessTokenCreated, McpAccessTokenListItem } from '@/models/mcp';

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

export const mcpAccessTokensApi = {
    list: () => httpClient.get<McpAccessTokenListItem[]>('/api/v1/me/mcp-access-tokens'),
    create: (body: { workspaceId: string; name?: string | null }) =>
        httpClient.post<McpAccessTokenCreated>('/api/v1/me/mcp-access-tokens', body),
    revoke: (tokenId: string) => httpClient.delete<void>(`/api/v1/me/mcp-access-tokens/${tokenId}`),
};

export const recipesApi = {
    getAll: (
        workspaceId: string,
        params?: { q?: string; page?: number; pageSize?: number; includeArchived?: boolean; orderBy?: string; direction?: 'asc' | 'desc' },
    ) => httpClient.get<PaginatedResponse<RecipeListItem>>(`/api/v1/workspaces/${workspaceId}/recipes`, { params }),
    getById: (workspaceId: string, recipeId: string) =>
        httpClient.get<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}`),
    create: (workspaceId: string, data: SaveRecipeRequest) =>
        httpClient.post<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes`, data),
    update: (workspaceId: string, recipeId: string, data: SaveRecipeRequest) =>
        httpClient.patch<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}`, data),
    remove: (workspaceId: string, recipeId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}`),
    previewImport: (workspaceId: string, url: string) =>
        httpClient.post<RecipeImportPreview>(`/api/v1/workspaces/${workspaceId}/recipes/import-preview`, { url }),
    uploadImage: (workspaceId: string, recipeId: string, file: File) => {
        const formData = new FormData();
        formData.append('file', file);
        return httpClient.postFormData<Recipe>(
            `/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/image`,
            formData,
        );
    },
    deleteImage: (workspaceId: string, recipeId: string) =>
        httpClient.delete<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/image`),
};

export const mealPlanApi = {
    getAll: (workspaceId: string, params?: { from?: string; to?: string }) =>
        httpClient.get<MealPlanEntry[]>(`/api/v1/workspaces/${workspaceId}/meal-plan-entries`, { params }),
    create: (workspaceId: string, data: SaveMealPlanEntryRequest) =>
        httpClient.post<MealPlanEntry>(`/api/v1/workspaces/${workspaceId}/meal-plan-entries`, data),
    update: (workspaceId: string, mealPlanEntryId: string, data: SaveMealPlanEntryRequest) =>
        httpClient.patch<MealPlanEntry>(`/api/v1/workspaces/${workspaceId}/meal-plan-entries/${mealPlanEntryId}`, data),
    remove: (workspaceId: string, mealPlanEntryId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/meal-plan-entries/${mealPlanEntryId}`),
};

export const shoppingListsApi = {
    getAll: (workspaceId: string) =>
        httpClient.get<ShoppingListListItem[]>(`/api/v1/workspaces/${workspaceId}/shopping-lists`),
    getById: (workspaceId: string, shoppingListId: string) =>
        httpClient.get<ShoppingList>(`/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}`),
    generate: (workspaceId: string, data: GenerateShoppingListRequest) =>
        httpClient.post<ShoppingList>(`/api/v1/workspaces/${workspaceId}/shopping-lists/generate`, data),
    update: (workspaceId: string, shoppingListId: string, data: SaveShoppingListRequest) =>
        httpClient.patch<ShoppingList>(`/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}`, data),
    remove: (workspaceId: string, shoppingListId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}`),
    createItem: (workspaceId: string, shoppingListId: string, data: SaveShoppingListItemRequest) =>
        httpClient.post<ShoppingListItem>(`/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}/items`, data),
    updateItem: (workspaceId: string, shoppingListId: string, itemId: string, data: SaveShoppingListItemRequest) =>
        httpClient.patch<ShoppingListItem>(
            `/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}/items/${itemId}`,
            data,
        ),
    removeItem: (workspaceId: string, shoppingListId: string, itemId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}/items/${itemId}`),
};
