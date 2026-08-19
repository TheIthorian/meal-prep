using Api.Endpoints.Requests.MealPrep;

namespace Api.Endpoints.Responses.MealPrep;

public record RecipeCollectionListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    int RecipeCount,
    Guid OwnerWorkspaceId,
    bool IsOwnedByViewerWorkspace
);

public record RecipeCollectionMembershipResponse(
    Guid CollectionId,
    string CollectionName,
    Guid OwnerWorkspaceId,
    bool IsOwnedByViewerWorkspace
);

public record RecipeCollectionSharedWorkspaceResponse(Guid WorkspaceId, string WorkspaceName);

public record RecipeCollectionDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerWorkspaceId,
    bool CanEdit,
    RecipeListItemResponse[] Recipes,
    RecipeCollectionSharedWorkspaceResponse[] SharedWithWorkspaces
);

public record RecipeCollectionExportRecipe(Guid RecipeId, string Title, string? ImageFileName, SaveRecipeRequest Payload);

public record RecipeCollectionExportResponse(
    string CollectionName,
    string? Description,
    DateTime ExportedAtUtc,
    RecipeCollectionExportRecipe[] Recipes
);

public record RecipeCollectionShareLinkResponse(string ShareToken, string ImportPath, DateTime CreatedAtUtc);

public record RecipeCollectionShareLinkPreviewResponse(
    string CollectionName,
    string? Description,
    string OwnerWorkspaceName,
    int RecipeCount,
    SharedRecipeSummaryResponse[] Recipes
);

/// <summary>
///     A recipe as seen through a share link. Deliberately not <see cref="RecipeResponse" />: that carries the owning
///     workspace id, the viewer's favourite flag and private collection membership, none of which an anonymous
///     visitor holding a share token may see.
/// </summary>
public record SharedRecipeSummaryResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal Servings,
    int? PrepMinutes,
    int? CookMinutes,
    string[] Tags,
    bool HasImage
);

/// <inheritdoc cref="SharedRecipeSummaryResponse" />
public record SharedRecipeDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal Servings,
    string? SourceUrl,
    string? Notes,
    int? PrepMinutes,
    int? CookMinutes,
    string[] Tags,
    bool HasImage,
    RecipeIngredientResponse[] Ingredients,
    RecipeStepResponse[] Steps,
    RecipeNutritionResponse? Nutrition
);

public record RecipeShareLinkResponse(string ShareToken, string SharePath, DateTime CreatedAtUtc);

/// <summary>
///     A single shared recipe as its recipient sees it: the recipe itself plus the name of the workspace that
///     shared it, and nothing that identifies that workspace.
/// </summary>
public record SharedRecipePreviewResponse(string OwnerWorkspaceName, SharedRecipeDetailResponse Recipe);
