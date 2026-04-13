using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Api.Endpoints.Middleware;

// @todo - is this still used??
public class SeedUserDataFilter(IServiceProvider services, UserManager<AppUser> userManager) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        // Run the identity endpoint first
        var result = await next(context);

        // If registration failed, stop
        if (result is not IResult r) return result;
        if (context.HttpContext.GetEndpoint()?.DisplayName is not null
            && !context.HttpContext.GetEndpoint()!.DisplayName!.Contains("POST /api/v1/auth/register")) return result;

        // Extract email from the request body
        var request = context.GetArgument<RegisterRequest>(0);


        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null) return result;

        // Do your seeding here (db context, services, etc.)
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var workspace = Workspace.CreateNew($"{user.UserName ?? user.Email}'s Workspace");
        var workspaceUser = WorkspaceUser.CreateNew(user, workspace, WorkspaceUser.Roles.Owner);

        db.Workspaces.Add(workspace);
        db.WorkspaceUsers.Add(workspaceUser);
        // await db.SaveChangesAsync();

        return result;
    }
}
