using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Represents a shopping list that belongs to a workspace.
/// </summary>
public class ShoppingList : DeletableWorkspaceEntity
{
    private ShoppingList() { }

    private ShoppingList(Workspace workspace, string name) : base(workspace) {
        Name = name;
    }

    public ICollection<ShoppingListItem> Items { get; private set; } = new List<ShoppingListItem>();
    public ICollection<ShoppingListSource> Sources { get; private set; } = new List<ShoppingListSource>();
    [MaxLength(255)] public string Name { get; private set; } = string.Empty;
    [MaxLength(2000)] public string? Notes { get; private set; }
    public DateTime? GeneratedAt { get; private set; }

    public static ShoppingList CreateNew(Workspace workspace, string name) {
        return new ShoppingList(workspace, name);
    }

    public void UpdateDetails(string name, string? notes) {
        Name = name;
        Notes = notes;
    }

    public void MarkGenerated(DateTime generatedAt) {
        GeneratedAt = generatedAt;
    }

    public void ReplaceItems(IEnumerable<ShoppingListItem> items) {
        Items.Clear();
        foreach (var item in items) Items.Add(item);
    }

    public void ReplaceSources(IEnumerable<ShoppingListSource> sources) {
        Sources.Clear();
        foreach (var source in sources) Sources.Add(source);
    }
}

/// <summary>
///     Represents one item on a shopping list.
/// </summary>
public class ShoppingListItem : Entity
{
    private ShoppingListItem() { }

    private ShoppingListItem(
        int sortOrder,
        string name,
        string displayText,
        decimal? amount,
        string? unit,
        string? normalizedIngredientName,
        bool isApproximate,
        bool isManual,
        string? category,
        string? note,
        string[] sourceNames
    ) {
        SortOrder = sortOrder;
        Name = name;
        DisplayText = displayText;
        Amount = amount;
        Unit = unit;
        NormalizedIngredientName = normalizedIngredientName;
        IsApproximate = isApproximate;
        IsManual = isManual;
        Category = category;
        Note = note;
        SourceNames = sourceNames;
    }

    public Guid ShoppingListId { get; private set; }
    public ShoppingList ShoppingList { get; private set; } = null!;
    public int SortOrder { get; private set; }
    [MaxLength(255)] public string Name { get; private set; } = string.Empty;
    [MaxLength(255)] public string? NormalizedIngredientName { get; private set; }
    public decimal? Amount { get; private set; }
    [MaxLength(64)] public string? Unit { get; private set; }
    public bool IsApproximate { get; private set; }
    public bool IsChecked { get; private set; }
    public bool IsManual { get; private set; }
    [MaxLength(255)] public string? Category { get; private set; }
    [MaxLength(255)] public string? Note { get; private set; }
    [MaxLength(1024)] public string DisplayText { get; private set; } = string.Empty;

    public string[] SourceNames { get; private set; } = [];

    public static ShoppingListItem CreateNew(
        int sortOrder,
        string name,
        string displayText,
        decimal? amount,
        string? unit,
        string? normalizedIngredientName,
        bool isApproximate,
        bool isManual,
        string? category,
        string? note,
        string[]? sourceNames = null
    ) {
        return new ShoppingListItem(
            sortOrder,
            name,
            displayText,
            amount,
            unit,
            normalizedIngredientName,
            isApproximate,
            isManual,
            category,
            note,
            sourceNames ?? []
        );
    }

    public void Update(
        string name,
        string displayText,
        decimal? amount,
        string? unit,
        string? normalizedIngredientName,
        bool isApproximate,
        bool isChecked,
        bool isManual,
        string? category,
        string? note,
        string[] sourceNames
    ) {
        Name = name;
        DisplayText = displayText;
        Amount = amount;
        Unit = unit;
        NormalizedIngredientName = normalizedIngredientName;
        IsApproximate = isApproximate;
        IsChecked = isChecked;
        IsManual = isManual;
        Category = category;
        Note = note;
        SourceNames = sourceNames;
    }
}

/// <summary>
///     Stores the origin of generated shopping-list content.
/// </summary>
public class ShoppingListSource : Entity
{
    private ShoppingListSource() { }

    private ShoppingListSource(Guid? recipeId, Guid? mealPlanEntryId, string sourceName) {
        RecipeId = recipeId;
        MealPlanEntryId = mealPlanEntryId;
        SourceName = sourceName;
    }

    public Guid ShoppingListId { get; private set; }
    public ShoppingList ShoppingList { get; private set; } = null!;
    public Guid? RecipeId { get; private set; }
    public Recipe? Recipe { get; private set; }
    public Guid? MealPlanEntryId { get; private set; }
    public MealPlanEntry? MealPlanEntry { get; private set; }
    [MaxLength(255)] public string SourceName { get; private set; } = string.Empty;

    public static ShoppingListSource CreateNew(Guid? recipeId, Guid? mealPlanEntryId, string sourceName) {
        return new ShoppingListSource(recipeId, mealPlanEntryId, sourceName);
    }
}
