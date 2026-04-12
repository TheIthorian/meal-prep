using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Stored request/response from the recipe-import LLM for debugging and history (workspace-scoped).
/// </summary>
public class RecipeImportAiLog : Entity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid? UserId { get; set; }

    [MaxLength(2048)] public string SourceUrl { get; set; } = string.Empty;

    [MaxLength(512)] public string ProviderBaseUrl { get; set; } = string.Empty;

    [MaxLength(256)] public string Model { get; set; } = string.Empty;

    /// <summary>
    ///     JSON describing messages and options sent to the chat API (no secrets).
    /// </summary>
    public string RequestJson { get; set; } = "{}";

    /// <summary>
    ///     Raw assistant message text (typically JSON) returned by the model.
    /// </summary>
    public string? ResponseContent { get; set; }

    [MaxLength(128)] public string? FinishReason { get; set; }

    public bool ParsedSuccessfully { get; set; }

    /// <summary>
    ///     Validation failure, transport error summary, or empty-prepared-html reason.
    /// </summary>
    public string? FailureDetail { get; set; }
}
