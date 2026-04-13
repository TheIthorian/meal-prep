using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Represents a workspace.
/// </summary>
public class Workspace : Entity
{
    private Workspace() { } // used by EF Core

    private Workspace(string name) {
        Name = name;
    }

    public ICollection<WorkspaceUser> Members { get; private set; } = new List<WorkspaceUser>();
    [MaxLength(1023)] public string Name { get; private set; } = string.Empty;

    public static Workspace CreateNew(string name) {
        return new Workspace(name);
    }

    public void Rename(string newName) {
        Name = newName;
    }
}
