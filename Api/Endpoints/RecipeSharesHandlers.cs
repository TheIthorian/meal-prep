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

/// <summary>
///     Sharing of a single recipe by link. The recipient reads it as a copy they cannot edit, and may save
///     their own copy into a workspace of theirs.
/// </summary>
internal static class RecipeSharesHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<RecipeShareLinkResponse>> PostCreateShareLink(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid recipeId,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var recipe = await db.Recipes
            .Where(value => value.Id == recipeId && value.WorkspaceId == workspaceId)
            .WhereIsNotDeleted()
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        // One link per recipe: re-sharing hands out the same URL so a link already sent to someone keeps
        // working rather than accumulating tokens that all point at the same recipe.
        var link = await db.RecipeShareLinks
            .Where(value => value.RecipeId == recipeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null) {
            link = RecipeShareLink.CreateNew(recipeId, workspaceUser.UserId, Guid.NewGuid().ToString("N"));
            await db.RecipeShareLinks.AddAsync(link, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Json(
            new RecipeShareLinkResponse(
                link.Token,
                $"/share/recipes/{link.Token}",
                DateTime.UtcNow
            )
        );
    }

    /// <remarks>
    ///     Anonymous by design: the share token is the credential, so a signed-out visitor who opens a magic link
    ///     can read the recipe and be invited to sign up. Only the recipe itself is exposed, never the workspace
    ///     it lives in, the viewer's favourites or its collection membership.
    /// </remarks>
    [AllowAnonymous]
    public static async Task<JsonHttpResult<SharedRecipePreviewResponse>> GetSharedRecipe(
        ApiDbContext db,
        string shareToken,
        CancellationToken cancellationToken
    ) {
        var link = await FindShareLinkAsync(
            db,
            shareToken,
            query => query
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Ingredients)
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Steps)
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Nutrition)
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Workspace),
            cancellationToken
        );

        return TypedResults.Json(
            new SharedRecipePreviewResponse(
                link.Recipe.Workspace.Name,
                link.Recipe.ToSharedRecipeDetailResponse()
            )
        );
    }

    /// <inheritdoc cref="GetSharedRecipe" />
    [AllowAnonymous]
    public static async Task<IResult> GetSharedRecipeImage(
        ApiDbContext db,
        RecipeImageStore recipeImageStore,
        HttpContext httpContext,
        string shareToken,
        [FromQuery] int? w,
        CancellationToken cancellationToken
    ) {
        var link = await FindShareLinkAsync(
            db,
            shareToken,
            query => query.Include(value => value.Recipe),
            cancellationToken
        );

        if (string.IsNullOrEmpty(link.Recipe.ImageObjectKey)) return TypedResults.NotFound();

        return await RecipeImageResults.ServeAsync(
            recipeImageStore,
            httpContext,
            link.Recipe.ImageObjectKey,
            w,
            cancellationToken
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostSaveSharedRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImageStore recipeImageStore,
        Guid workspaceId,
        string shareToken,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var link = await FindShareLinkAsync(
            db,
            shareToken,
            query => query
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Ingredients)
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Steps)
                .Include(value => value.Recipe).ThenInclude(recipe => recipe.Nutrition),
            cancellationToken
        );

        var savedRecipe = await RecipeCloner.CloneToWorkspaceAsync(
            link.Recipe,
            workspaceUser.Workspace,
            recipeImageStore,
            cancellationToken
        );

        await db.Recipes.AddAsync(savedRecipe, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(savedRecipe.ToRecipeResponse(false));
    }

    /// <summary>
    ///     Resolves a share token to its link. A token whose recipe has been deleted reads as an unknown token,
    ///     so a recipient never learns that a recipe used to be there.
    /// </summary>
    private static async Task<RecipeShareLink> FindShareLinkAsync(
        ApiDbContext db,
        string shareToken,
        Func<IQueryable<RecipeShareLink>, IQueryable<RecipeShareLink>> include,
        CancellationToken cancellationToken
    ) {
        var link = await include(db.RecipeShareLinks.AsNoTracking())
            .Where(value => value.Token == shareToken)
            .Where(value => !value.Recipe.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null) throw new EntityNotFoundException("Share link not found", null);

        return link;
    }
}
