using Api.Data;
using Api.Logging;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.MealPrep;

/// <summary>
///     Loads categories from <see cref="IngredientCategoryCache" /> and fills gaps using <see cref="IngredientCategoryLlmService" />.
/// </summary>
public sealed class IngredientCategoryResolutionService(
    ApiDbContext db,
    IngredientCategoryLlmService llm,
    ILogger<IngredientCategoryResolutionService> logger
) : IIngredientCategoryResolver
{
    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        IReadOnlyCollection<string> normalizedIngredientNames,
        CancellationToken cancellationToken = default
    )
    {
        var distinct = normalizedIngredientNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinct.Length == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var fromDb = await db.IngredientCategoryCaches.AsNoTracking()
            .Where(row => distinct.Contains(row.NormalizedIngredientName))
            .ToDictionaryAsync(row => row.NormalizedIngredientName, row => row.Category, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var missing = distinct.Where(name => !fromDb.ContainsKey(name)).ToArray();
        if (missing.Length == 0)
            return fromDb;

        var llmResult = await llm.TryCategorizeAsync(missing, cancellationToken).ConfigureAwait(false);
        if (llmResult is null || llmResult.Count == 0)
        {
            using (logger.BeginPropertyScope(("missingCount", missing.Length)))
            {
                logger.LogInformation("Ingredient categories skipped LLM or LLM returned no rows; using recipe sections only for uncached names.");
            }

            return fromDb;
        }

        foreach (var name in missing)
        {
            if (!llmResult.TryGetValue(name, out var category) || string.IsNullOrWhiteSpace(category))
                continue;

            category = category.Trim();
            if (category.Length > 128)
                category = category[..128];

            await db.IngredientCategoryCaches.AddAsync(
                    new IngredientCategoryCache(name, category),
                    cancellationToken
                )
                .ConfigureAwait(false);
            fromDb[name] = category;
        }

        return fromDb;
    }
}
