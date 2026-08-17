using Api.Data;
using Api.Domain;
using Api.Endpoints.Responses.MealPrep;
using Api.Models;
using Api.Services;
using Api.Services.MealPrep;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

internal static class RecipeCollectionImportJobsHandlers
{
    private const int MaxListedJobs = 20;

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionImportJobResponse>> PostStartImportJob(
        CurrentUserService currentUserService,
        RecipeCollectionImportJobService importJobService,
        RecipeCollectionImportJobQueue queue,
        Guid workspaceId,
        string shareToken,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var job = await importJobService.CreateJobAsync(
            workspaceUser.Workspace,
            workspaceUser.UserId,
            shareToken,
            cancellationToken
        );

        queue.Enqueue(job.Id);

        return TypedResults.Json(job.ToResponse(), statusCode: StatusCodes.Status202Accepted);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionImportJobResponse>> GetImportJob(
        CurrentUserService currentUserService,
        RecipeCollectionImportJobService importJobService,
        Guid workspaceId,
        Guid jobId,
        CancellationToken cancellationToken
    ) {
        await EnsureWorkspaceMemberAsync(currentUserService, workspaceId);

        var job = await importJobService.LoadJobAsync(jobId, cancellationToken);
        if (job is null || job.WorkspaceId != workspaceId)
            throw new EntityNotFoundException("Import job not found", null);

        return TypedResults.Json(job.ToResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionImportJobResponse[]>> GetImportJobs(
        CurrentUserService currentUserService,
        RecipeCollectionImportJobService importJobService,
        Guid workspaceId,
        [FromQuery] string? shareToken,
        CancellationToken cancellationToken
    ) {
        await EnsureWorkspaceMemberAsync(currentUserService, workspaceId);

        var jobs = await importJobService.ListJobsAsync(workspaceId, shareToken, MaxListedJobs, cancellationToken);

        return TypedResults.Json(jobs.Select(job => job.ToResponse()).ToArray());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionImportJobResponse>> PostRetryImportJob(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeCollectionImportJobService importJobService,
        RecipeCollectionImportJobQueue queue,
        Guid workspaceId,
        Guid jobId,
        CancellationToken cancellationToken
    ) {
        await EnsureWorkspaceMemberAsync(currentUserService, workspaceId);

        var job = await db.RecipeCollectionImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == jobId && value.WorkspaceId == workspaceId, cancellationToken);

        if (job is null) throw new EntityNotFoundException("Import job not found", null);

        if (!RecipeCollectionImportJobStatuses.IsTerminal(job.Status))
            throw new InvalidFormatException("Import still running", "Wait for the import to finish before retrying.");

        var requeued = await importJobService.RequeueFailedItemsAsync(job, cancellationToken);

        queue.Enqueue(requeued.Id);

        return TypedResults.Json(requeued.ToResponse(), statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task EnsureWorkspaceMemberAsync(CurrentUserService currentUserService, Guid workspaceId) {
        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);
    }
}
