using Api.Endpoints;
using Api.Endpoints.Middleware;
using Api.Endpoints.Requests.MealPrep;
using Api.Endpoints.Requests;
using Api.Endpoints.Responses.MealPrep;
using Api.Endpoints.Responses;
using Api.Models;

namespace Api.Startup;

/// <summary>
///     Maps the API endpoint groups and routes.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    extension(WebApplication app)
    {
        public void MapApiEndpoints() {
            app.MapGet("/api/health", () => Results.Ok())
                .WithName("Health");

            var authApiGroup = app.MapGroup("/api/v1/auth");
            authApiGroup.MapIdentityApi<AppUser>();
            authApiGroup.AddEndpointFilter<SeedUserDataFilter>();

            authApiGroup.MapPost("/logout", AuthHandlers.PostLogout)
                .WithName("Logout")
                .WithDescription("Revokes the current access and refresh tokens");

            authApiGroup.MapPost("/signup", AuthHandlers.PostRegister)
                .WithBodyValidation<RegisterRequest>()
                .WithName("Signup")
                .WithDescription("Registers a new user and creates a default workspace");

            var apiGroup = app.MapGroup("/api/v1");

            apiGroup.MapGet("/me", AuthHandlers.GetMe).Produces<UserResponse>().WithName("GetMe");

            apiGroup.MapPatch("/me", AuthHandlers.PatchMe)
                .WithBodyValidation<PatchUserRequest>()
                .Produces<UserResponse>()
                .WithName("UpdateMe");

            apiGroup.MapDelete("/me", AuthHandlers.DeleteMe)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .WithName("DeleteMe");

            apiGroup.MapPost("/me/mcp-access-tokens", McpAccessTokenHandlers.PostMcpAccessToken)
                .WithBodyValidation<PostMcpAccessTokenRequest>()
                .Produces<McpAccessTokenCreatedResponse>()
                .WithName("CreateMcpAccessToken");

            apiGroup.MapGet("/me/mcp-access-tokens", McpAccessTokenHandlers.GetMcpAccessTokens)
                .Produces<McpAccessTokenListItemResponse[]>()
                .WithName("ListMcpAccessTokens");

            apiGroup.MapDelete("/me/mcp-access-tokens/{tokenId:guid}", McpAccessTokenHandlers.DeleteMcpAccessToken)
                .WithName("RevokeMcpAccessToken");

            apiGroup.MapPost("/workspaces", WorkspacesHandlers.PostWorkspace)
                .WithBodyValidation<PostWorkspaceRequest>()
                .Produces<WorkspaceResponse>()
                .WithName("CreateWorkspace");

            apiGroup.MapGet("/workspaces", WorkspacesHandlers.GetWorkspaces)
                .Produces<WorkspaceResponse[]>()
                .WithName("GetWorkspaces");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}", WorkspacesHandlers.GetWorkspace)
                .Produces<WorkspaceResponse>()
                .WithName("GetWorkspace");

            apiGroup.MapPatch("/workspaces/{workspaceId:guid}", WorkspacesHandlers.PatchWorkspace)
                .WithBodyValidation<PostWorkspaceRequest>()
                .Produces<WorkspaceResponse>()
                .WithName("UpdateWorkspace");

            apiGroup.MapDelete("/workspaces/{workspaceId:guid}", WorkspacesHandlers.DeleteWorkspace)
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteWorkspace");

            apiGroup.MapPost("/workspaces/{workspaceId:guid}/members", WorkspacesHandlers.PostWorkspacesUser)
                .WithBodyValidation<PostWorkspaceUserRequest>()
                .Produces<MemberListItem>()
                .WithName("CreateWorkspaceUser");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/members/{userId:guid}",
                    WorkspacesHandlers.PatchWorkspaceUserRole
                )
                .WithBodyValidation<PatchWorkspaceUserRoleRequest>()
                .WithName("UpdateWorkspaceUserRole");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/members/{userId:guid}",
                    WorkspacesHandlers.DeleteWorkspaceUser
                )
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteWorkspaceUser");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}/recipes", RecipesHandlers.GetRecipes)
                .Produces<PaginatedResponse<RecipeListItemResponse>>()
                .WithName("GetRecipes");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}/recipe-tags", RecipesHandlers.GetRecipeTagWhitelist)
                .Produces<RecipeTagListResponse>()
                .WithName("GetRecipeTagWhitelist");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}/recipe-tags/usage", RecipesHandlers.GetRecipeTagUsage)
                .Produces<RecipeTagUsageListResponse>()
                .WithName("GetRecipeTagUsage");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipe-tags/bulk-remove",
                    RecipesHandlers.PostBulkRemoveRecipeTags
                )
                .WithBodyValidation<BulkRemoveRecipeTagsRequest>()
                .Produces<BulkRemoveRecipeTagsResponse>()
                .WithName("BulkRemoveRecipeTags");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipe-tags/remove-singletons",
                    RecipesHandlers.PostRemoveSingletonRecipeTags
                )
                .Produces<BulkRemoveRecipeTagsResponse>()
                .WithName("RemoveSingletonRecipeTags");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipes/suggest-tags",
                    RecipesHandlers.PostSuggestRecipeTags
                )
                .WithBodyValidation<SuggestRecipeTagsRequest>()
                .Produces<SuggestRecipeTagsResponse>()
                .WithName("SuggestRecipeTags");

            apiGroup.MapPost("/workspaces/{workspaceId:guid}/recipes/import-preview", RecipesHandlers.PostImportPreview)
                .WithBodyValidation<ImportRecipePreviewRequest>()
                .Produces<RecipeImportPreviewResponse>()
                .WithName("PreviewRecipeImport");

            apiGroup.MapPost("/workspaces/{workspaceId:guid}/recipes/import", RecipesHandlers.PostImportRecipe)
                .WithBodyValidation<ImportRecipeRequest>()
                .Produces<RecipeResponse>()
                .WithName("ImportRecipe");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}", RecipesHandlers.GetRecipe)
                .Produces<RecipeResponse>()
                .WithName("GetRecipe");

            apiGroup.MapPost("/workspaces/{workspaceId:guid}/recipes", RecipesHandlers.PostRecipe)
                .WithBodyValidation<SaveRecipeRequest>()
                .Produces<RecipeResponse>()
                .WithName("CreateRecipe");

            apiGroup.MapPatch("/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}", RecipesHandlers.PatchRecipe)
                .WithBodyValidation<SaveRecipeRequest>()
                .Produces<RecipeResponse>()
                .WithName("UpdateRecipe");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}/favorite",
                    RecipesHandlers.PatchRecipeFavorite
                )
                .WithBodyValidation<SetRecipeFavoriteRequest>()
                .Produces<RecipeResponse>()
                .WithName("SetRecipeFavorite");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}/autotag",
                    RecipesHandlers.PostAutotagRecipe
                )
                .Produces<RecipeResponse>()
                .WithName("AutotagRecipe");

            apiGroup.MapDelete("/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}", RecipesHandlers.DeleteRecipe)
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteRecipe");

            apiGroup.MapGet(
                    "/workspaces/{workspaceId:guid}/recipe-collections",
                    RecipeCollectionsHandlers.GetRecipeCollections
                )
                .Produces<RecipeCollectionListItemResponse[]>()
                .WithName("GetRecipeCollections");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipe-collections",
                    RecipeCollectionsHandlers.PostRecipeCollection
                )
                .WithBodyValidation<CreateRecipeCollectionRequest>()
                .Produces<RecipeCollectionDetailResponse>()
                .WithName("CreateRecipeCollection");

            apiGroup.MapGet(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}",
                    RecipeCollectionsHandlers.GetRecipeCollection
                )
                .Produces<RecipeCollectionDetailResponse>()
                .WithName("GetRecipeCollection");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}",
                    RecipeCollectionsHandlers.PatchRecipeCollection
                )
                .WithBodyValidation<PatchRecipeCollectionRequest>()
                .Produces<RecipeCollectionDetailResponse>()
                .WithName("UpdateRecipeCollection");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}",
                    RecipeCollectionsHandlers.DeleteRecipeCollection
                )
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteRecipeCollection");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}/recipes",
                    RecipeCollectionsHandlers.PostRecipeToCollection
                )
                .WithBodyValidation<AddRecipeToCollectionRequest>()
                .Produces<RecipeCollectionDetailResponse>()
                .WithName("AddRecipeToCollection");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}/recipes/{recipeId:guid}",
                    RecipeCollectionsHandlers.DeleteRecipeFromCollection
                )
                .Produces<RecipeCollectionDetailResponse>()
                .WithName("RemoveRecipeFromCollection");

            apiGroup.MapGet(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}/export",
                    RecipeCollectionsHandlers.GetRecipeCollectionExport
                )
                .Produces<RecipeCollectionExportResponse>()
                .WithName("ExportRecipeCollection");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}/share",
                    RecipeCollectionsHandlers.PostShareRecipeCollection
                )
                .WithBodyValidation<ShareRecipeCollectionRequest>()
                .Produces<RecipeCollectionSharedWorkspaceResponse[]>()
                .WithName("ShareRecipeCollection");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/recipe-collections/{collectionId:guid}/share/{targetWorkspaceId:guid}",
                    RecipeCollectionsHandlers.DeleteShareRecipeCollection
                )
                .Produces<RecipeCollectionSharedWorkspaceResponse[]>()
                .WithName("UnshareRecipeCollection");

            apiGroup.MapGet(
                    "/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}/image",
                    RecipesHandlers.GetRecipeImage
                )
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .WithName("GetRecipeImage");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}/image",
                    RecipesHandlers.PostRecipeImage
                )
                .DisableAntiforgery()
                .Produces<RecipeResponse>()
                .WithName("UploadRecipeImage");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/recipes/{recipeId:guid}/image",
                    RecipesHandlers.DeleteRecipeImage
                )
                .Produces<RecipeResponse>()
                .WithName("DeleteRecipeImage");

            apiGroup.MapGet(
                    "/workspaces/{workspaceId:guid}/meal-plan-entries",
                    MealPlanEntriesHandlers.GetMealPlanEntries
                )
                .Produces<MealPlanEntryResponse[]>()
                .WithName("GetMealPlanEntries");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/meal-plan-entries",
                    MealPlanEntriesHandlers.PostMealPlanEntry
                )
                .WithBodyValidation<SaveMealPlanEntryRequest>()
                .Produces<MealPlanEntryResponse>()
                .WithName("CreateMealPlanEntry");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/meal-plan-entries/{mealPlanEntryId:guid}",
                    MealPlanEntriesHandlers.PatchMealPlanEntry
                )
                .WithBodyValidation<SaveMealPlanEntryRequest>()
                .Produces<MealPlanEntryResponse>()
                .WithName("UpdateMealPlanEntry");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/meal-plan-entries/{mealPlanEntryId:guid}",
                    MealPlanEntriesHandlers.DeleteMealPlanEntry
                )
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteMealPlanEntry");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}/shopping-lists", ShoppingListsHandlers.GetShoppingLists)
                .Produces<ShoppingListListItemResponse[]>()
                .WithName("GetShoppingLists");

            apiGroup.MapGet(
                    "/workspaces/{workspaceId:guid}/shopping-lists/{shoppingListId:guid}",
                    ShoppingListsHandlers.GetShoppingList
                )
                .Produces<ShoppingListResponse>()
                .WithName("GetShoppingList");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/shopping-lists/generate",
                    ShoppingListsHandlers.PostGenerateShoppingList
                )
                .WithBodyValidation<GenerateShoppingListRequest>()
                .Produces<ShoppingListResponse>()
                .WithName("GenerateShoppingList");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/shopping-lists/{shoppingListId:guid}",
                    ShoppingListsHandlers.PatchShoppingList
                )
                .WithBodyValidation<SaveShoppingListRequest>()
                .Produces<ShoppingListResponse>()
                .WithName("UpdateShoppingList");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/shopping-lists/{shoppingListId:guid}",
                    ShoppingListsHandlers.DeleteShoppingList
                )
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteShoppingList");

            apiGroup.MapPost(
                    "/workspaces/{workspaceId:guid}/shopping-lists/{shoppingListId:guid}/items",
                    ShoppingListsHandlers.PostShoppingListItem
                )
                .WithBodyValidation<SaveShoppingListItemRequest>()
                .Produces<ShoppingListItemResponse>()
                .WithName("CreateShoppingListItem");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/shopping-lists/{shoppingListId:guid}/items/{itemId:guid}",
                    ShoppingListsHandlers.PatchShoppingListItem
                )
                .WithBodyValidation<SaveShoppingListItemRequest>()
                .Produces<ShoppingListItemResponse>()
                .WithName("UpdateShoppingListItem");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/shopping-lists/{shoppingListId:guid}/items/{itemId:guid}",
                    ShoppingListsHandlers.DeleteShoppingListItem
                )
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteShoppingListItem");
        }
    }
}
