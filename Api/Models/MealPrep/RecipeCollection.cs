using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     A user-defined grouping of recipes within a workspace.
/// </summary>
public class RecipeCollection : DeletableWorkspaceEntity
{
    private RecipeCollection() { }

    private RecipeCollection(Workspace workspace, string name, string? description) : base(workspace) {
        Name = name;
        Description = description;
    }

    public ICollection<RecipeCollectionRecipe> RecipeLinks { get; private set; } = new List<RecipeCollectionRecipe>();
    public ICollection<RecipeCollectionShare> Shares { get; private set; } = new List<RecipeCollectionShare>();

    [MaxLength(255)] public string Name { get; private set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; private set; }

    public static RecipeCollection CreateNew(Workspace workspace, string name, string? description) {
        return new RecipeCollection(workspace, name, description);
    }

    public void UpdateDetails(string name, string? description) {
        Name = name;
        Description = description;
    }
}

/// <summary>
///     Associates a recipe with a collection. Recipes must belong to the same workspace as the collection.
/// </summary>
public class RecipeCollectionRecipe : Entity
{
    private RecipeCollectionRecipe() { }

    private RecipeCollectionRecipe(Guid recipeCollectionId, Guid recipeId, int sortOrder) {
        RecipeCollectionId = recipeCollectionId;
        RecipeId = recipeId;
        SortOrder = sortOrder;
    }

    public Guid RecipeCollectionId { get; private set; }
    public RecipeCollection RecipeCollection { get; private set; } = null!;
    public Guid RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;
    public int SortOrder { get; private set; }

    public static RecipeCollectionRecipe CreateNew(Guid recipeCollectionId, Guid recipeId, int sortOrder) {
        return new RecipeCollectionRecipe(recipeCollectionId, recipeId, sortOrder);
    }
}

/// <summary>
///     Grants another workspace visibility of a recipe collection (same account typically belongs to both).
/// </summary>
public class RecipeCollectionShare : Entity
{
    private RecipeCollectionShare() { }

    private RecipeCollectionShare(Guid recipeCollectionId, Guid sharedWithWorkspaceId) {
        RecipeCollectionId = recipeCollectionId;
        SharedWithWorkspaceId = sharedWithWorkspaceId;
    }

    public Guid RecipeCollectionId { get; private set; }
    public RecipeCollection RecipeCollection { get; private set; } = null!;
    public Guid SharedWithWorkspaceId { get; private set; }
    public Workspace SharedWithWorkspace { get; private set; } = null!;

    public static RecipeCollectionShare CreateNew(Guid recipeCollectionId, Guid sharedWithWorkspaceId) {
        return new RecipeCollectionShare(recipeCollectionId, sharedWithWorkspaceId);
    }
}
