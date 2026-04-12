using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Represents one planned meal in a workspace calendar.
/// </summary>
public class MealPlanEntry : DeletableWorkspaceEntity
{
    private MealPlanEntry() { }

    private MealPlanEntry(Workspace workspace, Recipe recipe, DateOnly plannedDate, string mealType) : base(workspace) {
        Recipe = recipe;
        RecipeId = recipe.Id;
        PlannedDate = plannedDate;
        MealType = mealType;
    }

    public Recipe Recipe { get; private set; } = null!;
    public Guid RecipeId { get; private set; }
    public DateOnly PlannedDate { get; private set; }
    [MaxLength(64)] public string MealType { get; private set; } = MealPlanEntryMealTypes.Dinner;
    public decimal? TargetServings { get; private set; }
    [MaxLength(2000)] public string? Notes { get; private set; }
    [MaxLength(64)] public string Status { get; private set; } = MealPlanEntryStatuses.Planned;

    public static MealPlanEntry CreateNew(Workspace workspace, Recipe recipe, DateOnly plannedDate, string mealType) {
        return new MealPlanEntry(workspace, recipe, plannedDate, mealType);
    }

    public void ChangeRecipe(Recipe recipe) {
        Recipe = recipe;
        RecipeId = recipe.Id;
    }

    public void Update(DateOnly plannedDate, string mealType, decimal? targetServings, string? notes, string status) {
        PlannedDate = plannedDate;
        MealType = mealType;
        TargetServings = targetServings;
        Notes = notes;
        Status = status;
    }
}

/// <summary>
///     Defines the supported meal types for planned meals.
/// </summary>
public static class MealPlanEntryMealTypes
{
    public const string Breakfast = "breakfast";
    public const string Lunch = "lunch";
    public const string Dinner = "dinner";
    public const string Snack = "snack";

    public static readonly string[] All = [Breakfast, Lunch, Dinner, Snack];
}

/// <summary>
///     Defines the supported statuses for planned meals.
/// </summary>
public static class MealPlanEntryStatuses
{
    public const string Planned = "planned";
    public const string Completed = "completed";

    public static readonly string[] All = [Planned, Completed];
}
