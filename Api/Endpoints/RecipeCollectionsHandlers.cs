using Api.Data;
using Api.Domain;
using Api.Endpoints.Requests.MealPrep;
using Api.Endpoints.Responses.MealPrep;
using Api.Models;
using Api.Services;
using Api.Services.MealPrep;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

internal static class RecipeCollectionsHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionListItemResponse[]>> GetRecipeCollections(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        CancellationToken cancellationToken
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var userId = currentUserId.Value;

        var owned = await db.RecipeCollections
            .AsNoTracking()
            .Where(collection => collection.WorkspaceId == workspaceId && !collection.IsDeleted)
            .ForCurrentUser(userId)
            .OrderBy(collection => collection.Name)
            .Select(collection => new RecipeCollectionListItemResponse(
                    collection.Id,
                    collection.Name,
                    collection.Description,
                    collection.RecipeLinks.Count,
                    collection.WorkspaceId,
                    true
                )
            )
            .ToArrayAsync(cancellationToken);

        var shared = await db.RecipeCollectionShares
            .AsNoTracking()
            .Where(share => share.SharedWithWorkspaceId == workspaceId)
            .Where(share => !share.RecipeCollection.IsDeleted)
            .Where(share => share.RecipeCollection.Workspace.Members.Any(member => member.UserId == userId))
            .OrderBy(share => share.RecipeCollection.Name)
            .Select(share => new RecipeCollectionListItemResponse(
                    share.RecipeCollection.Id,
                    share.RecipeCollection.Name,
                    share.RecipeCollection.Description,
                    share.RecipeCollection.RecipeLinks.Count,
                    share.RecipeCollection.WorkspaceId,
                    false
                )
            )
            .ToArrayAsync(cancellationToken);

        var merged = owned
            .Concat(shared)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.Name)
            .ToArray();

        return TypedResults.Json(merged);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionDetailResponse>> GetRecipeCollection(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        CancellationToken cancellationToken
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var resolved = await TryResolveCollectionAsync(db, currentUserId.Value, workspaceId, collectionId, cancellationToken);
        if (resolved.Collection is null) throw new EntityNotFoundException("Collection not found", null);

        var collection = resolved.Collection;
        var canEdit = resolved.CanEdit;
        var userId = currentUserId.Value;
        var ownerWorkspaceId = collection.WorkspaceId;

        var orderedLinks = await db.RecipeCollectionRecipes
            .AsNoTracking()
            .Where(link => link.RecipeCollectionId == collectionId)
            .OrderBy(link => link.SortOrder)
            .Select(link => link.RecipeId)
            .ToArrayAsync(cancellationToken);

        var recipes = await db.Recipes
            .AsNoTracking()
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Steps)
            .Where(recipe => orderedLinks.Contains(recipe.Id) && recipe.WorkspaceId == ownerWorkspaceId)
            .WhereIsNotDeleted()
            .ForCurrentUser(userId)
            .ToArrayAsync(cancellationToken);

        var order = orderedLinks.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index);
        recipes = recipes.OrderBy(recipe => order.GetValueOrDefault(recipe.Id, int.MaxValue)).ToArray();

        var listItems = new List<RecipeListItemResponse>();
        foreach (var recipe in recipes) {
            var isFavorite = await RecipeIsFavoriteAsync(db, userId, recipe.Id, cancellationToken);
            listItems.Add(recipe.ToRecipeListItemResponse(isFavorite));
        }

        RecipeCollectionSharedWorkspaceResponse[] sharedWith = [];
        if (canEdit) {
            sharedWith = await db.RecipeCollectionShares
                .AsNoTracking()
                .Where(share => share.RecipeCollectionId == collectionId)
                .OrderBy(share => share.SharedWithWorkspace.Name)
                .Select(share => new RecipeCollectionSharedWorkspaceResponse(
                        share.SharedWithWorkspaceId,
                        share.SharedWithWorkspace.Name
                    )
                )
                .ToArrayAsync(cancellationToken);
        }

        return TypedResults.Json(
            new RecipeCollectionDetailResponse(
                collection.Id,
                collection.Name,
                collection.Description,
                ownerWorkspaceId,
                canEdit,
                listItems.ToArray(),
                sharedWith
            )
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionDetailResponse>> PostRecipeCollection(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        [FromBody] CreateRecipeCollectionRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var collection = RecipeCollection.CreateNew(workspaceUser.Workspace, body.Name, body.Description);
        await db.RecipeCollections.AddAsync(collection, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(
            new RecipeCollectionDetailResponse(
                collection.Id,
                collection.Name,
                collection.Description,
                collection.WorkspaceId,
                true,
                [],
                []
            )
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionDetailResponse>> PatchRecipeCollection(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        [FromBody] PatchRecipeCollectionRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var collection = await db.RecipeCollections
            .Where(c => c.Id == collectionId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .ForCurrentUser(workspaceUser.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null) throw new EntityNotFoundException("Collection not found", null);

        collection.UpdateDetails(body.Name, body.Description);
        await db.SaveChangesAsync(cancellationToken);

        return await GetRecipeCollection(currentUserService, db, workspaceId, collectionId, cancellationToken);
    }

    [Authorize]
    public static async Task<Ok> DeleteRecipeCollection(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var updated = await db.RecipeCollections
            .Where(c => c.Id == collectionId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .ForCurrentUser(workspaceUser.UserId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.IsDeleted, true).SetProperty(c => c.UpdatedAt, DateTime.UtcNow),
                cancellationToken
            );

        if (updated == 0) throw new EntityNotFoundException("Collection not found", null);

        return TypedResults.Ok();
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionDetailResponse>> PostRecipeToCollection(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        [FromBody] AddRecipeToCollectionRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var userId = workspaceUser.UserId;

        var collection = await db.RecipeCollections
            .Where(c => c.Id == collectionId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .ForCurrentUser(userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null) throw new EntityNotFoundException("Collection not found", null);

        var recipeExists = await db.Recipes
            .WhereIsNotDeleted()
            .ForCurrentUser(userId)
            .AnyAsync(
                recipe => recipe.Id == body.RecipeId && recipe.WorkspaceId == workspaceId,
                cancellationToken
            );

        if (!recipeExists) throw new EntityNotFoundException("Recipe not found", null);

        var already = await db.RecipeCollectionRecipes.AnyAsync(
            link => link.RecipeCollectionId == collectionId && link.RecipeId == body.RecipeId,
            cancellationToken
        );

        if (!already) {
            var nextOrder = await db.RecipeCollectionRecipes
                .Where(link => link.RecipeCollectionId == collectionId)
                .Select(link => (int?)link.SortOrder)
                .MaxAsync(cancellationToken) ?? -1;

            await db.RecipeCollectionRecipes.AddAsync(
                RecipeCollectionRecipe.CreateNew(collectionId, body.RecipeId, nextOrder + 1),
                cancellationToken
            );
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetRecipeCollection(currentUserService, db, workspaceId, collectionId, cancellationToken);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionDetailResponse>> DeleteRecipeFromCollection(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        Guid recipeId,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var userId = workspaceUser.UserId;

        var ownsCollection = await db.RecipeCollections
            .AnyAsync(
                c => c.Id == collectionId && c.WorkspaceId == workspaceId && !c.IsDeleted,
                cancellationToken
            );

        if (!ownsCollection) throw new EntityNotFoundException("Collection not found", null);

        var authorized = await db.RecipeCollections
            .Where(c => c.Id == collectionId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .ForCurrentUser(userId)
            .AnyAsync(cancellationToken);

        if (!authorized) throw new EntityNotFoundException("Collection not found", null);

        await db.RecipeCollectionRecipes
            .Where(link => link.RecipeCollectionId == collectionId && link.RecipeId == recipeId)
            .ExecuteDeleteAsync(cancellationToken);

        return await GetRecipeCollection(currentUserService, db, workspaceId, collectionId, cancellationToken);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionExportResponse>> GetRecipeCollectionExport(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        CancellationToken cancellationToken
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var resolved = await TryResolveCollectionAsync(db, currentUserId.Value, workspaceId, collectionId, cancellationToken);
        if (resolved.Collection is null) throw new EntityNotFoundException("Collection not found", null);

        var collection = resolved.Collection;
        var ownerWorkspaceId = collection.WorkspaceId;
        var userId = currentUserId.Value;

        var orderedLinks = await db.RecipeCollectionRecipes
            .AsNoTracking()
            .Where(link => link.RecipeCollectionId == collectionId)
            .OrderBy(link => link.SortOrder)
            .Select(link => link.RecipeId)
            .ToArrayAsync(cancellationToken);

        var recipes = await db.Recipes
            .AsNoTracking()
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Steps)
            .Include(recipe => recipe.Nutrition)
            .Where(recipe => orderedLinks.Contains(recipe.Id) && recipe.WorkspaceId == ownerWorkspaceId)
            .WhereIsNotDeleted()
            .ForCurrentUser(userId)
            .ToArrayAsync(cancellationToken);

        var order = orderedLinks.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index);
        recipes = recipes.OrderBy(recipe => order.GetValueOrDefault(recipe.Id, int.MaxValue)).ToArray();

        var exportRecipes = recipes
            .Select(recipe => new RecipeCollectionExportRecipe(
                    recipe.Id,
                    recipe.Title,
                    string.IsNullOrEmpty(recipe.ImageObjectKey) ? null : $"{recipe.Id}.webp",
                    RecipeExportMapper.ToSaveRecipeRequest(recipe)
                )
            )
            .ToArray();

        return TypedResults.Json(
            new RecipeCollectionExportResponse(
                collection.Name,
                collection.Description,
                DateTime.UtcNow,
                exportRecipes
            )
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionShareLinkResponse>> PostCreateShareLink(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid collectionId,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var collection = await db.RecipeCollections
            .Where(c => c.Id == collectionId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .ForCurrentUser(workspaceUser.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null) throw new EntityNotFoundException("Collection not found", null);

        var token = Guid.NewGuid().ToString("N");
        var link = RecipeCollectionShareLink.CreateNew(collectionId, workspaceUser.UserId, token);
        await db.RecipeCollectionShareLinks.AddAsync(link, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(
            new RecipeCollectionShareLinkResponse(
                token,
                $"/share/recipe-collections/{token}",
                DateTime.UtcNow
            )
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionShareLinkPreviewResponse>> GetShareLinkPreview(
        CurrentUserService currentUserService,
        ApiDbContext db,
        string shareToken,
        CancellationToken cancellationToken
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var link = await db.RecipeCollectionShareLinks
            .AsNoTracking()
            .Where(link => link.Token == shareToken)
            .Include(link => link.RecipeCollection)
            .ThenInclude(collection => collection.Workspace)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null || link.RecipeCollection.IsDeleted)
            throw new EntityNotFoundException("Share link not found", null);

        var recipeCount = await db.RecipeCollectionRecipes
            .AsNoTracking()
            .CountAsync(recipe => recipe.RecipeCollectionId == link.RecipeCollectionId, cancellationToken);

        return TypedResults.Json(
            new RecipeCollectionShareLinkPreviewResponse(
                link.RecipeCollection.Name,
                link.RecipeCollection.Description,
                link.RecipeCollection.Workspace.Name,
                recipeCount
            )
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeCollectionDetailResponse>> PostImportFromShareLink(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        string shareToken,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var link = await db.RecipeCollectionShareLinks
            .Where(value => value.Token == shareToken)
            .Include(value => value.RecipeCollection)
            .ThenInclude(collection => collection.Workspace)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null || link.RecipeCollection.IsDeleted)
            throw new EntityNotFoundException("Share link not found", null);

        var sourceCollection = link.RecipeCollection;
        var sourceRecipeIds = await db.RecipeCollectionRecipes
            .AsNoTracking()
            .Where(value => value.RecipeCollectionId == sourceCollection.Id)
            .OrderBy(value => value.SortOrder)
            .Select(value => value.RecipeId)
            .ToArrayAsync(cancellationToken);

        var sourceRecipes = await db.Recipes
            .AsNoTracking()
            .WhereIsNotDeleted()
            .Where(value => sourceRecipeIds.Contains(value.Id) && value.WorkspaceId == sourceCollection.WorkspaceId)
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ToArrayAsync(cancellationToken);

        var sourceOrder = sourceRecipeIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
        sourceRecipes = sourceRecipes.OrderBy(value => sourceOrder.GetValueOrDefault(value.Id, int.MaxValue)).ToArray();

        var targetCollection = RecipeCollection.CreateNew(
            workspaceUser.Workspace,
            $"{sourceCollection.Name} (Imported)",
            sourceCollection.Description
        );
        await db.RecipeCollections.AddAsync(targetCollection, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var importedRecipes = new List<Recipe>();
        var sortOrder = 0;
        foreach (var sourceRecipe in sourceRecipes) {
            var importedRecipe = await CloneRecipeToWorkspaceAsync(
                sourceRecipe,
                workspaceUser.Workspace,
                s3StorageService,
                cancellationToken
            );
            importedRecipes.Add(importedRecipe);
            await db.Recipes.AddAsync(importedRecipe, cancellationToken);
            await db.RecipeCollectionRecipes.AddAsync(
                RecipeCollectionRecipe.CreateNew(targetCollection.Id, importedRecipe.Id, sortOrder++),
                cancellationToken
            );
        }

        await db.SaveChangesAsync(cancellationToken);

        var listItems = importedRecipes
            .Select(recipe => recipe.ToRecipeListItemResponse(isFavorite: false))
            .ToArray();

        return TypedResults.Json(
            new RecipeCollectionDetailResponse(
                targetCollection.Id,
                targetCollection.Name,
                targetCollection.Description,
                targetCollection.WorkspaceId,
                true,
                listItems,
                []
            )
        );
    }

    private static async Task<Recipe> CloneRecipeToWorkspaceAsync(
        Recipe sourceRecipe,
        Workspace targetWorkspace,
        IS3StorageService s3StorageService,
        CancellationToken cancellationToken
    ) {
        var recipe = Recipe.CreateNew(targetWorkspace, sourceRecipe.Title, sourceRecipe.Servings);
        recipe.UpdateDetails(
            sourceRecipe.Title,
            sourceRecipe.Description,
            sourceRecipe.Servings,
            sourceRecipe.SourceUrl,
            sourceRecipe.Notes,
            sourceRecipe.PrepMinutes,
            sourceRecipe.CookMinutes,
            sourceRecipe.IsArchived,
            RecipeTagWhitelist.NormalizeToWhitelist(sourceRecipe.Tags)
        );
        recipe.ReplaceIngredients(
            sourceRecipe.Ingredients
                .OrderBy(value => value.SortOrder)
                .Select((value, index) => RecipeIngredient.CreateNew(
                        index,
                        value.Name,
                        value.DisplayText,
                        value.Amount,
                        value.Unit,
                        value.NormalizedIngredientName,
                        value.PreparationNote,
                        value.Section
                    )
                )
        );
        recipe.ReplaceSteps(
            sourceRecipe.Steps
                .OrderBy(value => value.SortOrder)
                .Select((value, index) => RecipeStep.CreateNew(index, value.Instruction, value.TimerSeconds))
        );
        recipe.SetNutrition(
            sourceRecipe.NutritionServingBasis,
            sourceRecipe.Nutrition
                .Select(value => RecipeNutrition.CreateNew(value.NutrientType, value.Amount))
                .ToArray()
        );

        if (!string.IsNullOrEmpty(sourceRecipe.ImageObjectKey)) {
            await using var imageStream = await s3StorageService.DownloadFileAsync(sourceRecipe.ImageObjectKey);
            var imageObjectKey = await s3StorageService.UploadFileAsync(
                imageStream,
                $"{recipe.Id}.webp",
                "image/webp"
            );
            recipe.SetImageObjectKey(imageObjectKey);
        }

        return recipe;
    }

    private static async Task<(RecipeCollection? Collection, bool CanEdit)> TryResolveCollectionAsync(
        ApiDbContext db,
        Guid userId,
        Guid routeWorkspaceId,
        Guid collectionId,
        CancellationToken cancellationToken
    ) {
        var owned = await db.RecipeCollections
            .AsNoTracking()
            .Include(collection => collection.Shares)
            .Where(collection => collection.Id == collectionId && collection.WorkspaceId == routeWorkspaceId && !collection.IsDeleted)
            .ForCurrentUser(userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (owned is not null) return (owned, true);

        var shared = await db.RecipeCollections
            .AsNoTracking()
            .Include(collection => collection.Shares)
            .Include(collection => collection.Workspace)
            .ThenInclude(workspace => workspace.Members)
            .Where(collection => collection.Id == collectionId && !collection.IsDeleted)
            .Where(collection => collection.Shares.Any(share => share.SharedWithWorkspaceId == routeWorkspaceId))
            .Where(collection => collection.Workspace.Members.Any(member => member.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

        if (shared is not null) return (shared, false);

        return (null, false);
    }

    private static Task<bool> RecipeIsFavoriteAsync(
        ApiDbContext db,
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken
    ) {
        return db.RecipeFavorites.AsNoTracking()
            .AnyAsync(favorite => favorite.UserId == userId && favorite.RecipeId == recipeId, cancellationToken);
    }
}
