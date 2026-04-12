using Microsoft.EntityFrameworkCore;

namespace Api.Models;

public abstract class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
}

/// <summary>
///     Provides a base type for entities that belong to a workspace.
/// </summary>
public abstract class WorkspaceEntity : Entity
{
    protected WorkspaceEntity() { } // used by EF Core

    protected WorkspaceEntity(Workspace workspace) {
        WorkspaceId = workspace.Id;
        Workspace = workspace;
    }

    public Guid WorkspaceId { get; private set; }
    public Workspace Workspace { get; private set; } = null!;
}

[Index(nameof(WorkspaceId), nameof(IsDeleted))]
/// <summary>
/// Provides a workspace-scoped entity base type with soft-delete support.
/// </summary>
public abstract class DeletableWorkspaceEntity : WorkspaceEntity
{
    protected DeletableWorkspaceEntity() { }
    protected DeletableWorkspaceEntity(Workspace workspace) : base(workspace) { }
    public bool IsDeleted { get; set; } = false;
}
