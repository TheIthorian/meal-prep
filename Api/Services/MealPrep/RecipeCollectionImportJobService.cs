using Api.Data;
using Api.Domain;
using Api.Endpoints;
using Api.Logging;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.MealPrep;

/// <summary>
///     Creates and executes collection import jobs. Job state lives in the database so an import that
///     outlives the browser tab that started it can still be reported on, retried, and finished.
/// </summary>
public class RecipeCollectionImportJobService(
    ApiDbContext db,
    IS3StorageService s3StorageService,
    ILogger<RecipeCollectionImportJobService> logger
)
{
    /// <summary>
    ///     Snapshots the shared collection's recipes into a new pending job. Does not import anything;
    ///     call <see cref="RunAsync" /> (normally via the queue) to do the work.
    /// </summary>
    public async Task<RecipeCollectionImportJob> CreateJobAsync(
        Workspace targetWorkspace,
        Guid startedByUserId,
        string shareToken,
        CancellationToken cancellationToken
    ) {
        var link = await db.RecipeCollectionShareLinks
            .Where(value => value.Token == shareToken)
            .Include(value => value.RecipeCollection)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null || link.RecipeCollection.IsDeleted)
            throw new EntityNotFoundException("Share link not found", null);

        var sourceCollection = link.RecipeCollection;

        var sourceRecipes = await db.RecipeCollectionRecipes
            .AsNoTracking()
            .Where(value => value.RecipeCollectionId == sourceCollection.Id)
            .Where(value => !value.Recipe.IsDeleted && value.Recipe.WorkspaceId == sourceCollection.WorkspaceId)
            .OrderBy(value => value.SortOrder)
            .Select(value => new { value.RecipeId, value.Recipe.Title })
            .ToArrayAsync(cancellationToken);

        var job = RecipeCollectionImportJob.CreateNew(
            targetWorkspace,
            startedByUserId,
            shareToken,
            sourceCollection.Name
        );

        await db.RecipeCollectionImportJobs.AddAsync(job, cancellationToken);

        var sortOrder = 0;
        foreach (var sourceRecipe in sourceRecipes)
            await db.RecipeCollectionImportJobItems.AddAsync(
                RecipeCollectionImportJobItem.CreateNew(job.Id, sourceRecipe.RecipeId, sourceRecipe.Title, sortOrder++),
                cancellationToken
            );

        await db.SaveChangesAsync(cancellationToken);

        using var scope = logger.BeginPropertyScope(
            ("workspaceId", targetWorkspace.Id),
            ("importJobId", job.Id),
            ("recipeCount", sourceRecipes.Length)
        );

        logger.LogInformation("Queued recipe collection import job");

        return await LoadJobAsync(job.Id, cancellationToken)
               ?? throw new EntityNotFoundException("Import job not found", null);
    }

    /// <summary>
    ///     Resets every recipe that did not import back to pending so the job can be run again. Already
    ///     imported recipes are left alone, so a retry never duplicates them.
    /// </summary>
    public async Task<RecipeCollectionImportJob> RequeueFailedItemsAsync(
        RecipeCollectionImportJob job,
        CancellationToken cancellationToken
    ) {
        var failedItems = await db.RecipeCollectionImportJobItems
            .Where(item => item.RecipeCollectionImportJobId == job.Id)
            .Where(item => item.Status != RecipeCollectionImportItemStatuses.Imported)
            .ToArrayAsync(cancellationToken);

        if (failedItems.Length == 0)
            throw new InvalidFormatException("Nothing to retry", "This import has no failed recipes.");

        foreach (var item in failedItems) item.ResetForRetry();

        var tracked = await db.RecipeCollectionImportJobs
            .FirstAsync(value => value.Id == job.Id, cancellationToken);
        tracked.MarkQueuedForRetry();

        await db.SaveChangesAsync(cancellationToken);

        using var scope = logger.BeginPropertyScope(
            ("workspaceId", job.WorkspaceId),
            ("importJobId", job.Id),
            ("retryCount", failedItems.Length)
        );

        logger.LogInformation("Requeued failed recipes for collection import job");

        return await LoadJobAsync(job.Id, cancellationToken)
               ?? throw new EntityNotFoundException("Import job not found", null);
    }

    /// <summary>
    ///     Imports every pending recipe on the job, recording per-recipe success or failure as it goes so
    ///     that a poller sees progress advance. Never throws for a single bad recipe.
    /// </summary>
    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken) {
        var job = await db.RecipeCollectionImportJobs
            .Include(value => value.Workspace)
            .FirstOrDefaultAsync(value => value.Id == jobId, cancellationToken);

        if (job is null) return;

        using var loggingScope = logger.BeginPropertyScope(
            ("workspaceId", job.WorkspaceId),
            ("importJobId", job.Id)
        );

        job.MarkRunning();
        await db.SaveChangesAsync(cancellationToken);

        try {
            var collectionId = await EnsureTargetCollectionAsync(job, cancellationToken);

            var pendingItemIds = await db.RecipeCollectionImportJobItems
                .AsNoTracking()
                .Where(item => item.RecipeCollectionImportJobId == jobId)
                .Where(item => item.Status == RecipeCollectionImportItemStatuses.Pending)
                .OrderBy(item => item.SortOrder)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);

            foreach (var itemId in pendingItemIds) {
                cancellationToken.ThrowIfCancellationRequested();
                await ImportItemAsync(jobId, collectionId, itemId, cancellationToken);
            }

            var hasFailures = await db.RecipeCollectionImportJobItems
                .AnyAsync(
                    item => item.RecipeCollectionImportJobId == job.Id
                            && item.Status == RecipeCollectionImportItemStatuses.Failed,
                    cancellationToken
                );

            // Individual recipe failures clear the change tracker, so re-attach before the final write.
            var trackedJob = await db.RecipeCollectionImportJobs.FirstAsync(
                value => value.Id == jobId,
                cancellationToken
            );
            trackedJob.MarkFinished(hasFailures);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Finished recipe collection import job with status {Status}", trackedJob.Status);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            db.ChangeTracker.Clear();

            var trackedJob = await db.RecipeCollectionImportJobs.FirstAsync(value => value.Id == jobId);
            trackedJob.MarkFailed(exception.Message);
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogError(exception, "Recipe collection import job failed");
        }
    }

    /// <summary>
    ///     Loads a job together with its per-recipe items, which the response mapper needs.
    /// </summary>
    public Task<RecipeCollectionImportJob?> LoadJobAsync(Guid jobId, CancellationToken cancellationToken) {
        return db.RecipeCollectionImportJobs
            .AsNoTracking()
            .Include(job => job.Items)
            .FirstOrDefaultAsync(job => job.Id == jobId, cancellationToken);
    }

    /// <summary>
    ///     Lists a workspace's most recent import jobs, newest first, so the UI can pick a run back up
    ///     after a reload.
    /// </summary>
    public Task<List<RecipeCollectionImportJob>> ListJobsAsync(
        Guid workspaceId,
        string? shareToken,
        int limit,
        CancellationToken cancellationToken
    ) {
        var query = db.RecipeCollectionImportJobs
            .AsNoTracking()
            .Include(job => job.Items)
            .Where(job => job.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(shareToken)) query = query.Where(job => job.ShareToken == shareToken);

        return query
            .OrderByDescending(job => job.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Re-queues jobs left mid-flight by a process restart. Running jobs are reset to pending because
    ///     completed recipes are already persisted, so a re-run only picks up what is still outstanding.
    /// </summary>
    public async Task<Guid[]> RecoverInterruptedJobsAsync(CancellationToken cancellationToken) {
        var interrupted = await db.RecipeCollectionImportJobs
            .Where(job => job.Status == RecipeCollectionImportJobStatuses.Running
                          || job.Status == RecipeCollectionImportJobStatuses.Pending
            )
            .ToArrayAsync(cancellationToken);

        foreach (var job in interrupted) job.MarkQueuedForRetry();

        if (interrupted.Length > 0) await db.SaveChangesAsync(cancellationToken);

        return interrupted.Select(job => job.Id).ToArray();
    }

    private async Task<Guid> EnsureTargetCollectionAsync(
        RecipeCollectionImportJob job,
        CancellationToken cancellationToken
    ) {
        if (job.TargetRecipeCollectionId is Guid existingId) return existingId;

        var sourceCollection = await db.RecipeCollectionShareLinks
            .AsNoTracking()
            .Where(link => link.Token == job.ShareToken)
            .Select(link => new { link.RecipeCollection.Name, link.RecipeCollection.Description })
            .FirstOrDefaultAsync(cancellationToken);

        var collection = RecipeCollection.CreateNew(
            job.Workspace,
            $"{sourceCollection?.Name ?? job.SourceCollectionName} (Imported)",
            sourceCollection?.Description
        );

        await db.RecipeCollections.AddAsync(collection, cancellationToken);

        job.AttachTargetCollection(collection.Id);
        await db.SaveChangesAsync(cancellationToken);

        return collection.Id;
    }

    private async Task ImportItemAsync(
        Guid jobId,
        Guid targetCollectionId,
        Guid itemId,
        CancellationToken cancellationToken
    ) {
        // Each recipe starts from a clean tracker so one bad recipe cannot poison the rest of the run.
        db.ChangeTracker.Clear();

        var item = await db.RecipeCollectionImportJobItems.FirstAsync(
            value => value.Id == itemId,
            cancellationToken
        );

        var targetWorkspace = await db.RecipeCollectionImportJobs
            .Where(value => value.Id == jobId)
            .Select(value => value.Workspace)
            .FirstAsync(cancellationToken);

        try {
            var sourceRecipe = await db.Recipes
                .AsNoTracking()
                .WhereIsNotDeleted()
                .Where(recipe => recipe.Id == item.SourceRecipeId)
                .Include(recipe => recipe.Ingredients)
                .Include(recipe => recipe.Steps)
                .Include(recipe => recipe.Nutrition)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceRecipe is null) {
                item.MarkFailed("The recipe is no longer available in the shared collection.");
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var importedRecipe = await RecipeCollectionsHandlers.CloneRecipeToWorkspaceAsync(
                sourceRecipe,
                targetWorkspace,
                s3StorageService,
                cancellationToken
            );

            await db.Recipes.AddAsync(importedRecipe, cancellationToken);
            await db.RecipeCollectionRecipes.AddAsync(
                RecipeCollectionRecipe.CreateNew(targetCollectionId, importedRecipe.Id, item.SortOrder),
                cancellationToken
            );

            item.MarkImported(importedRecipe.Id);
            await db.SaveChangesAsync(cancellationToken);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            db.ChangeTracker.Clear();

            var trackedItem = await db.RecipeCollectionImportJobItems
                .FirstAsync(value => value.Id == itemId, cancellationToken);
            trackedItem.MarkFailed(exception.Message);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning(exception, "Failed to import recipe {SourceRecipeId}", item.SourceRecipeId);
        }
    }
}
