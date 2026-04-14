import { PostWorkspaceRequest, WorkspaceResponse } from '@/models/workspace';
import { httpClient } from './http-client';
import { LoginRequest, RegisterRequest } from '@/models/auth';
import { UserResponse } from '@/models/user';
import {
    GenerateShoppingListRequest,
    NextMeal,
    Recipe,
    RecipeListItem,
    SaveNextMealRequest,
    BulkRemoveRecipeTagsResponse,
    RecipeTagListResponse,
    RecipeTagUsageListResponse,
    SaveRecipeRequest,
    SuggestRecipeTagsRequest,
    SuggestRecipeTagsResponse,
    SaveShoppingListItemRequest,
    SaveShoppingListRequest,
    ShoppingList,
    ShoppingListItem,
    ShoppingListListItem,
    RecipeCollectionListItem,
    RecipeCollectionDetail,
    CreateRecipeCollectionRequest,
    PatchRecipeCollectionRequest,
    RecipeCollectionExport,
    RecipeCollectionShareLink,
    RecipeCollectionShareLinkPreview,
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
        params?: {
            q?: string;
            page?: number;
            pageSize?: number;
            includeArchived?: boolean;
            orderBy?: string;
            direction?: 'asc' | 'desc';
        },
    ) => httpClient.get<PaginatedResponse<RecipeListItem>>(`/api/v1/workspaces/${workspaceId}/recipes`, { params }),
    getById: (workspaceId: string, recipeId: string) =>
        httpClient.get<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}`),
    create: (workspaceId: string, data: SaveRecipeRequest) =>
        httpClient.post<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes`, data),
    update: (workspaceId: string, recipeId: string, data: SaveRecipeRequest) =>
        httpClient.patch<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}`, data),
    setFavorite: (workspaceId: string, recipeId: string, isFavorite: boolean) =>
        httpClient.patch<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/favorite`, { isFavorite }),
    autotag: (workspaceId: string, recipeId: string) =>
        httpClient.post<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/autotag`, {}),
    remove: (workspaceId: string, recipeId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}`),
    importFromUrl: (workspaceId: string, url: string) =>
        httpClient.post<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/import`, { url }),
    uploadImage: (workspaceId: string, recipeId: string, file: File) => {
        const formData = new FormData();
        formData.append('file', file);
        return httpClient.postFormData<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/image`, formData);
    },
    deleteImage: (workspaceId: string, recipeId: string) =>
        httpClient.delete<Recipe>(`/api/v1/workspaces/${workspaceId}/recipes/${recipeId}/image`),
    getTagWhitelist: (workspaceId: string) =>
        httpClient.get<RecipeTagListResponse>(`/api/v1/workspaces/${workspaceId}/recipe-tags`),
    suggestTags: (workspaceId: string, data: SuggestRecipeTagsRequest) =>
        httpClient.post<SuggestRecipeTagsResponse>(`/api/v1/workspaces/${workspaceId}/recipes/suggest-tags`, data),
    getRecipeTagUsage: (workspaceId: string) =>
        httpClient.get<RecipeTagUsageListResponse>(`/api/v1/workspaces/${workspaceId}/recipe-tags/usage`),
    bulkRemoveRecipeTags: (workspaceId: string, tags: string[]) =>
        httpClient.post<BulkRemoveRecipeTagsResponse>(`/api/v1/workspaces/${workspaceId}/recipe-tags/bulk-remove`, {
            tags,
        }),
    removeSingletonRecipeTags: (workspaceId: string) =>
        httpClient.post<BulkRemoveRecipeTagsResponse>(
            `/api/v1/workspaces/${workspaceId}/recipe-tags/remove-singletons`,
            {},
        ),
};

export const recipeCollectionsApi = {
    list: (workspaceId: string) =>
        httpClient.get<RecipeCollectionListItem[]>(`/api/v1/workspaces/${workspaceId}/recipe-collections`),
    create: (workspaceId: string, data: CreateRecipeCollectionRequest) =>
        httpClient.post<RecipeCollectionDetail>(`/api/v1/workspaces/${workspaceId}/recipe-collections`, data),
    get: (workspaceId: string, collectionId: string) =>
        httpClient.get<RecipeCollectionDetail>(
            `/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}`,
        ),
    update: (workspaceId: string, collectionId: string, data: PatchRecipeCollectionRequest) =>
        httpClient.patch<RecipeCollectionDetail>(
            `/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}`,
            data,
        ),
    remove: (workspaceId: string, collectionId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}`),
    addRecipe: (workspaceId: string, collectionId: string, recipeId: string) =>
        httpClient.post<RecipeCollectionDetail>(
            `/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}/recipes`,
            { recipeId },
        ),
    removeRecipe: (workspaceId: string, collectionId: string, recipeId: string) =>
        httpClient.delete<RecipeCollectionDetail>(
            `/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}/recipes/${recipeId}`,
        ),
    exportJson: (workspaceId: string, collectionId: string) =>
        httpClient.get<RecipeCollectionExport>(
            `/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}/export`,
        ),
    createShareLink: (workspaceId: string, collectionId: string) =>
        httpClient.post<RecipeCollectionShareLink>(
            `/api/v1/workspaces/${workspaceId}/recipe-collections/${collectionId}/share`,
            {},
        ),
    getShareLinkPreview: (shareToken: string) =>
        httpClient.get<RecipeCollectionShareLinkPreview>(`/api/v1/recipe-collection-share/${shareToken}`),
    importFromShareLink: (workspaceId: string, shareToken: string) =>
        httpClient.post<RecipeCollectionDetail>(`/api/v1/workspaces/${workspaceId}/recipe-collection-import/${shareToken}`, {}),
};

export const mealPlanApi = {
    getAll: (workspaceId: string, params?: { from?: string; to?: string }) =>
        httpClient.get<NextMeal[]>(`/api/v1/workspaces/${workspaceId}/next-meals`, { params }),
    create: (workspaceId: string, data: SaveNextMealRequest) =>
        httpClient.post<NextMeal>(`/api/v1/workspaces/${workspaceId}/next-meals`, data),
    update: (workspaceId: string, nextMealId: string, data: SaveNextMealRequest) =>
        httpClient.patch<NextMeal>(`/api/v1/workspaces/${workspaceId}/next-meals/${nextMealId}`, data),
    remove: (workspaceId: string, nextMealId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/next-meals/${nextMealId}`),
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
        httpClient.post<ShoppingListItem>(
            `/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}/items`,
            data,
        ),
    updateItem: (workspaceId: string, shoppingListId: string, itemId: string, data: SaveShoppingListItemRequest) =>
        httpClient.patch<ShoppingListItem>(
            `/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}/items/${itemId}`,
            data,
        ),
    removeItem: (workspaceId: string, shoppingListId: string, itemId: string) =>
        httpClient.delete<void>(`/api/v1/workspaces/${workspaceId}/shopping-lists/${shoppingListId}/items/${itemId}`),
};
