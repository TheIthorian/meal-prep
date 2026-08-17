using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     The lifecycle states a collection import job can be in.
/// </summary>
public static class RecipeCollectionImportJobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string CompletedWithErrors = "completedWithErrors";
    public const string Failed = "failed";

    public static bool IsTerminal(string status) {
        return status is Completed or CompletedWithErrors or Failed;
    }
}

/// <summary>
///     The states an individual recipe within a collection import job can be in.
/// </summary>
public static class RecipeCollectionImportItemStatuses
{
    public const string Pending = "pending";
    public const string Imported = "imported";
    public const string Failed = "failed";
}

/// <summary>
///     Tracks a long-running import of a shared recipe collection into a workspace so that progress
///     survives the request that started it, and can be reported back after a page reload.
/// </summary>
public class RecipeCollectionImportJob : WorkspaceEntity
{
    private RecipeCollectionImportJob() { } // used by EF Core

    private RecipeCollectionImportJob(
        Workspace workspace,
        Guid startedByUserId,
        string shareToken,
        string sourceCollectionName
    ) : base(workspace) {
        StartedByUserId = startedByUserId;
        ShareToken = shareToken;
        SourceCollectionName = sourceCollectionName;
        Status = RecipeCollectionImportJobStatuses.Pending;
    }

    public Guid StartedByUserId { get; private set; }
    public AppUser StartedByUser { get; private set; } = null!;

    [MaxLength(128)] public string ShareToken { get; private set; } = string.Empty;
    [MaxLength(255)] public string SourceCollectionName { get; private set; } = string.Empty;
    [MaxLength(32)] public string Status { get; private set; } = RecipeCollectionImportJobStatuses.Pending;

    public Guid? TargetRecipeCollectionId { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    [MaxLength(2000)] public string? ErrorMessage { get; private set; }

    public ICollection<RecipeCollectionImportJobItem> Items { get; private set; } =
        new List<RecipeCollectionImportJobItem>();

    public static RecipeCollectionImportJob CreateNew(
        Workspace workspace,
        Guid startedByUserId,
        string shareToken,
        string sourceCollectionName
    ) {
        return new RecipeCollectionImportJob(workspace, startedByUserId, shareToken, sourceCollectionName);
    }

    public void MarkRunning() {
        Status = RecipeCollectionImportJobStatuses.Running;
        CompletedAt = null;
        ErrorMessage = null;
    }

    public void MarkQueuedForRetry() {
        Status = RecipeCollectionImportJobStatuses.Pending;
        CompletedAt = null;
        ErrorMessage = null;
    }

    public void AttachTargetCollection(Guid recipeCollectionId) {
        TargetRecipeCollectionId = recipeCollectionId;
    }

    public void MarkFinished(bool hasFailures) {
        Status = hasFailures
            ? RecipeCollectionImportJobStatuses.CompletedWithErrors
            : RecipeCollectionImportJobStatuses.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage) {
        Status = RecipeCollectionImportJobStatuses.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
    }
}

/// <summary>
///     One source recipe belonging to a collection import job. Carries the per-recipe outcome so the UI
///     can list what failed and offer a retry.
/// </summary>
public class RecipeCollectionImportJobItem : Entity
{
    private RecipeCollectionImportJobItem() { } // used by EF Core

    private RecipeCollectionImportJobItem(
        Guid recipeCollectionImportJobId,
        Guid sourceRecipeId,
        string recipeTitle,
        int sortOrder
    ) {
        RecipeCollectionImportJobId = recipeCollectionImportJobId;
        SourceRecipeId = sourceRecipeId;
        RecipeTitle = recipeTitle;
        SortOrder = sortOrder;
        Status = RecipeCollectionImportItemStatuses.Pending;
    }

    public Guid RecipeCollectionImportJobId { get; private set; }
    public RecipeCollectionImportJob RecipeCollectionImportJob { get; private set; } = null!;

    public Guid SourceRecipeId { get; private set; }
    public int SortOrder { get; private set; }

    [MaxLength(255)] public string RecipeTitle { get; private set; } = string.Empty;
    [MaxLength(32)] public string Status { get; private set; } = RecipeCollectionImportItemStatuses.Pending;
    [MaxLength(1000)] public string? ErrorMessage { get; private set; }

    public Guid? ImportedRecipeId { get; private set; }

    public static RecipeCollectionImportJobItem CreateNew(
        Guid recipeCollectionImportJobId,
        Guid sourceRecipeId,
        string recipeTitle,
        int sortOrder
    ) {
        return new RecipeCollectionImportJobItem(recipeCollectionImportJobId, sourceRecipeId, recipeTitle, sortOrder);
    }

    public void MarkImported(Guid importedRecipeId) {
        Status = RecipeCollectionImportItemStatuses.Imported;
        ImportedRecipeId = importedRecipeId;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage) {
        Status = RecipeCollectionImportItemStatuses.Failed;
        ImportedRecipeId = null;
        ErrorMessage = errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage;
    }

    public void ResetForRetry() {
        Status = RecipeCollectionImportItemStatuses.Pending;
        ErrorMessage = null;
    }
}
