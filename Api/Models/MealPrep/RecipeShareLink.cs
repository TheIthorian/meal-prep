using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     A share token for a single recipe. Holding the token grants read-only access to that recipe and
///     the right to save a copy into a workspace of the recipient's own.
/// </summary>
public class RecipeShareLink : Entity
{
    private RecipeShareLink() { }

    private RecipeShareLink(Guid recipeId, Guid createdByUserId, string token) {
        RecipeId = recipeId;
        CreatedByUserId = createdByUserId;
        Token = token;
    }

    public Guid RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public AppUser CreatedByUser { get; private set; } = null!;

    [MaxLength(128)] public string Token { get; private set; } = string.Empty;

    public static RecipeShareLink CreateNew(Guid recipeId, Guid createdByUserId, string token) {
        return new RecipeShareLink(recipeId, createdByUserId, token);
    }
}
