using Api.Models;

namespace Api.Endpoints.Responses.MealPrep;

public record RecipeCollectionImportFailureResponse(
    Guid SourceRecipeId,
    string RecipeTitle,
    string? ErrorMessage
);

public record RecipeCollectionImportJobResponse(
    Guid Id,
    Guid WorkspaceId,
    string Status,
    string ShareToken,
    string SourceCollectionName,
    int TotalRecipes,
    int ProcessedRecipes,
    int ImportedRecipes,
    int FailedRecipes,
    Guid? TargetCollectionId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    RecipeCollectionImportFailureResponse[] Failures
);

/// <summary>
///     Maps collection import jobs to API responses.
/// </summary>
public static class RecipeCollectionImportJobResponseTransforms
{
    extension(RecipeCollectionImportJob job)
    {
        /// <remarks>Requires <see cref="RecipeCollectionImportJob.Items" /> to be loaded.</remarks>
        public RecipeCollectionImportJobResponse ToResponse() {
            var items = job.Items.OrderBy(item => item.SortOrder).ToArray();
            var imported = items.Count(item => item.Status == RecipeCollectionImportItemStatuses.Imported);
            var failedItems = items
                .Where(item => item.Status == RecipeCollectionImportItemStatuses.Failed)
                .ToArray();

            return new RecipeCollectionImportJobResponse(
                job.Id,
                job.WorkspaceId,
                job.Status,
                job.ShareToken,
                job.SourceCollectionName,
                items.Length,
                imported + failedItems.Length,
                imported,
                failedItems.Length,
                job.TargetRecipeCollectionId,
                job.CreatedAt,
                job.CompletedAt,
                job.ErrorMessage,
                failedItems
                    .Select(item => new RecipeCollectionImportFailureResponse(
                            item.SourceRecipeId,
                            item.RecipeTitle,
                            item.ErrorMessage
                        )
                    )
                    .ToArray()
            );
        }
    }
}
