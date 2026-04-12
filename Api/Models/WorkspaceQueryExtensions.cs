using Microsoft.EntityFrameworkCore;

namespace Api.Models;

/// <summary>
///     Provides helpers for filtering workspace-owned queries.
/// </summary>
public static class WorkspaceQueryExtensions
{
    extension<T>(IQueryable<T> query) where T : WorkspaceEntity
    {
        public IQueryable<T> ForCurrentUser(Guid? userId) {
            return query.Include(e => e.Workspace)
                .ThenInclude(w => w.Members)
                .Where(e => e.Workspace.Members.Any(m => m.UserId.Equals(userId)));
        }
    }

    extension<T>(IQueryable<T> query) where T : DeletableWorkspaceEntity
    {
        public IQueryable<T> WhereIsNotDeleted() {
            return query.Where(e => e.IsDeleted != true);
        }
    }

    extension(IQueryable<Workspace> query)
    {
        public IQueryable<Workspace> ForCurrentUser(Guid? userId) {
            return query.Include(w => w.Members).Where(w => w.Members.Any(m => m.UserId.Equals(userId)));
        }
    }
}
