using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Represents a recipe that belongs to a workspace.
/// </summary>
public class Recipe : DeletableWorkspaceEntity
{
    private Recipe() { }

    private Recipe(Workspace workspace, string title, decimal servings) : base(workspace) {
        Title = title;
        Servings = servings;
    }

    public ICollection<RecipeIngredient> Ingredients { get; private set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; private set; } = new List<RecipeStep>();
    public ICollection<RecipeNutrition> Nutrition { get; private set; } = new List<RecipeNutrition>();

    [MaxLength(255)] public string Title { get; private set; } = string.Empty;
    [MaxLength(4000)] public string? Description { get; private set; }
    public decimal Servings { get; private set; } = 1m;
    [MaxLength(2048)] public string? SourceUrl { get; private set; }
    [MaxLength(4000)] public string? Notes { get; private set; }
    public int? PrepMinutes { get; private set; }
    public int? CookMinutes { get; private set; }
    public bool IsArchived { get; private set; }
    public decimal? NutritionServingBasis { get; private set; }
    public string[] Tags { get; private set; } = [];

    /// <summary>
    ///     S3 object key for the recipe cover image, when set.
    /// </summary>
    [MaxLength(512)] public string? ImageObjectKey { get; private set; }

    public static Recipe CreateNew(Workspace workspace, string title, decimal servings) {
        return new Recipe(workspace, title, servings);
    }

    public void UpdateDetails(
        string title,
        string? description,
        decimal servings,
        string? sourceUrl,
        string? notes,
        int? prepMinutes,
        int? cookMinutes,
        bool isArchived,
        IReadOnlyCollection<string> tags
    ) {
        Title = title;
        Description = description;
        Servings = servings;
        SourceUrl = sourceUrl;
        Notes = notes;
        PrepMinutes = prepMinutes;
        CookMinutes = cookMinutes;
        IsArchived = isArchived;
        Tags = tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag)
            .ToArray();
    }

    public void SetImageObjectKey(string? objectKey) {
        ImageObjectKey = objectKey;
    }

    public void ReplaceIngredients(IEnumerable<RecipeIngredient> ingredients) {
        Ingredients.Clear();
        foreach (var ingredient in ingredients) Ingredients.Add(ingredient);
    }

    public void ReplaceSteps(IEnumerable<RecipeStep> steps) {
        Steps.Clear();
        foreach (var step in steps) Steps.Add(step);
    }

    public void SetNutrition(decimal? servingBasis, IEnumerable<RecipeNutrition> nutrients) {
        NutritionServingBasis = servingBasis;
        Nutrition.Clear();

        foreach (var nutrient in nutrients) Nutrition.Add(nutrient);
    }
}

/// <summary>
///     Represents a structured ingredient belonging to a recipe.
/// </summary>
public class RecipeIngredient : Entity
{
    private RecipeIngredient() { }

    private RecipeIngredient(
        int sortOrder,
        string name,
        string displayText,
        decimal? amount,
        string? unit,
        string? normalizedIngredientName,
        string? preparationNote,
        string? section
    ) {
        SortOrder = sortOrder;
        Name = name;
        DisplayText = displayText;
        Amount = amount;
        Unit = unit;
        NormalizedIngredientName = normalizedIngredientName;
        PreparationNote = preparationNote;
        Section = section;
    }

    public Guid RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;
    public int SortOrder { get; private set; }
    [MaxLength(255)] public string Name { get; private set; } = string.Empty;
    [MaxLength(255)] public string? NormalizedIngredientName { get; private set; }
    public decimal? Amount { get; private set; }
    [MaxLength(64)] public string? Unit { get; private set; }
    [MaxLength(255)] public string? PreparationNote { get; private set; }
    [MaxLength(255)] public string? Section { get; private set; }
    [MaxLength(1024)] public string DisplayText { get; private set; } = string.Empty;

    public static RecipeIngredient CreateNew(
        int sortOrder,
        string name,
        string displayText,
        decimal? amount,
        string? unit,
        string? normalizedIngredientName,
        string? preparationNote,
        string? section
    ) {
        return new RecipeIngredient(
            sortOrder,
            name,
            displayText,
            amount,
            unit,
            normalizedIngredientName,
            preparationNote,
            section
        );
    }
}

/// <summary>
///     Represents one ordered step in a recipe.
/// </summary>
public class RecipeStep : Entity
{
    private RecipeStep() { }

    private RecipeStep(int sortOrder, string instruction, int? timerSeconds) {
        SortOrder = sortOrder;
        Instruction = instruction;
        TimerSeconds = timerSeconds;
    }

    public Guid RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;
    public int SortOrder { get; private set; }
    [MaxLength(4000)] public string Instruction { get; private set; } = string.Empty;
    public int? TimerSeconds { get; private set; }

    public static RecipeStep CreateNew(int sortOrder, string instruction, int? timerSeconds) {
        return new RecipeStep(sortOrder, instruction, timerSeconds);
    }
}

/// <summary>
///     Stores one nutrient measurement for a recipe.
/// </summary>
public class RecipeNutrition : Entity
{
    private RecipeNutrition() { }

    private RecipeNutrition(string nutrientType, decimal amount) {
        NutrientType = nutrientType;
        Amount = amount;
    }

    public Guid RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;
    [MaxLength(64)] public string NutrientType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    public static RecipeNutrition CreateNew(string nutrientType, decimal amount) {
        return new RecipeNutrition(NormalizeNutrientType(nutrientType), amount);
    }

    private static string NormalizeNutrientType(string nutrientType) {
        return nutrientType.Trim().ToLowerInvariant();
    }
}

/// <summary>
///     Defines the built-in nutrient types supported by the recipe API.
/// </summary>
public static class RecipeNutrientTypes
{
    public const string Calories = "calories";
    public const string Protein = "protein";
    public const string Carbohydrate = "carbohydrate";
    public const string Fat = "fat";
    public const string Fiber = "fiber";
    public const string Sugar = "sugar";
    public const string Sodium = "sodium";

    public static readonly string[] DefaultOrder = [Calories, Protein, Carbohydrate, Fat, Fiber, Sugar, Sodium];
}
