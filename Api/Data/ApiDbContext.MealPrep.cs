using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public partial class ApiDbContext
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeNutrition> RecipeNutrition => Set<RecipeNutrition>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<ShoppingListSource> ShoppingListSources => Set<ShoppingListSource>();
    public DbSet<RecipeImportAiLog> RecipeImportAiLogs => Set<RecipeImportAiLog>();
    public DbSet<IngredientCategoryCache> IngredientCategoryCaches => Set<IngredientCategoryCache>();
    public DbSet<McpPersonalAccessToken> McpPersonalAccessTokens => Set<McpPersonalAccessToken>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Recipe>()
            .HasIndex(recipe => new { recipe.WorkspaceId, recipe.IsDeleted, recipe.IsArchived });
        modelBuilder.Entity<Recipe>().Property(recipe => recipe.Servings).HasPrecision(10, 2);
        modelBuilder.Entity<Recipe>().Property(recipe => recipe.NutritionServingBasis).HasPrecision(10, 2);
        modelBuilder.Entity<Recipe>().Property(recipe => recipe.Tags).HasColumnType("text[]");

        modelBuilder.Entity<RecipeIngredient>().Property(ingredient => ingredient.Amount).HasPrecision(10, 3);
        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(ingredient => ingredient.Recipe)
            .WithMany(recipe => recipe.Ingredients)
            .HasForeignKey(ingredient => ingredient.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeStep>()
            .HasOne(step => step.Recipe)
            .WithMany(recipe => recipe.Steps)
            .HasForeignKey(step => step.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeNutrition>()
            .HasOne(nutrition => nutrition.Recipe)
            .WithMany(recipe => recipe.Nutrition)
            .HasForeignKey(nutrition => nutrition.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RecipeNutrition>().HasIndex(value => new { value.RecipeId, value.NutrientType }).IsUnique();
        modelBuilder.Entity<RecipeNutrition>().Property(value => value.Amount).HasPrecision(10, 2);

        modelBuilder.Entity<MealPlanEntry>()
            .HasIndex(entry => new { entry.WorkspaceId, entry.PlannedDate, entry.IsDeleted });
        modelBuilder.Entity<MealPlanEntry>().Property(entry => entry.TargetServings).HasPrecision(10, 2);
        modelBuilder.Entity<MealPlanEntry>()
            .HasOne(entry => entry.Recipe)
            .WithMany()
            .HasForeignKey(entry => entry.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ShoppingList>().HasIndex(list => new { list.WorkspaceId, list.IsDeleted });

        modelBuilder.Entity<ShoppingListItem>().Property(item => item.Amount).HasPrecision(10, 3);
        modelBuilder.Entity<ShoppingListItem>().Property(item => item.SourceNames).HasColumnType("text[]");
        modelBuilder.Entity<ShoppingListItem>()
            .HasOne(item => item.ShoppingList)
            .WithMany(list => list.Items)
            .HasForeignKey(item => item.ShoppingListId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShoppingListSource>()
            .HasOne(source => source.ShoppingList)
            .WithMany(list => list.Sources)
            .HasForeignKey(source => source.ShoppingListId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShoppingListSource>()
            .HasOne(source => source.Recipe)
            .WithMany()
            .HasForeignKey(source => source.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ShoppingListSource>()
            .HasOne(source => source.MealPlanEntry)
            .WithMany()
            .HasForeignKey(source => source.MealPlanEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecipeImportAiLog>()
            .HasOne(log => log.Workspace)
            .WithMany()
            .HasForeignKey(log => log.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeImportAiLog>().HasIndex(log => new { log.WorkspaceId, log.CreatedAt });

        modelBuilder.Entity<IngredientCategoryCache>()
            .HasIndex(cache => cache.NormalizedIngredientName)
            .IsUnique();

        modelBuilder.Entity<McpPersonalAccessToken>()
            .HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<McpPersonalAccessToken>()
            .HasOne(token => token.Workspace)
            .WithMany()
            .HasForeignKey(token => token.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
