namespace Api.Models;

/// <summary>
///     Associates a user with a recipe they have marked as a favorite (per user, not workspace-wide).
/// </summary>
public class RecipeFavorite
{
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public AppUser User { get; set; } = null!;
    public Recipe Recipe { get; set; } = null!;
}
