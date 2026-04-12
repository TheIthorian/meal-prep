using Api.Endpoints;
using Api.Endpoints.Middleware;
using Api.Endpoints.Requests;
using Api.Endpoints.Responses;
using Api.Models;

namespace Api.Startup;

/// <summary>
///     Maps the API endpoint groups and routes.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    extension(WebApplication app)
    {
        public void MapApiEndpoints() {
            var authApiGroup = app.MapGroup("/api/v1/auth");
            authApiGroup.MapIdentityApi<AppUser>();
            authApiGroup.AddEndpointFilter<SeedUserDataFilter>();

            authApiGroup.MapPost("/logout", AuthHandlers.PostLogout)
                .WithName("Logout")
                .WithDescription("Revokes the current access and refresh tokens");

            authApiGroup.MapPost("/signup", AuthHandlers.PostRegister)
                .WithBodyValidation<RegisterRequest>()
                .WithName("Signup")
                .WithDescription("Registers a new user and creates a default workspace");

            var apiGroup = app.MapGroup("/api/v1");

            apiGroup.MapGet("/me", AuthHandlers.GetMe).Produces<UserResponse>().WithName("GetMe");

            apiGroup.MapPatch("/me", AuthHandlers.PatchMe)
                .WithBodyValidation<PatchUserRequest>()
                .Produces<UserResponse>()
                .WithName("UpdateMe");

            apiGroup.MapDelete("/me", AuthHandlers.DeleteMe)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .WithName("DeleteMe");

            apiGroup.MapPost("/workspaces", WorkspacesHandlers.PostWorkspace)
                .WithBodyValidation<PostWorkspaceRequest>()
                .Produces<WorkspaceResponse>()
                .WithName("CreateWorkspace");

            apiGroup.MapGet("/workspaces", WorkspacesHandlers.GetWorkspaces)
                .Produces<WorkspaceResponse[]>()
                .WithName("GetWorkspaces");

            apiGroup.MapGet("/workspaces/{workspaceId:guid}", WorkspacesHandlers.GetWorkspace)
                .Produces<WorkspaceResponse>()
                .WithName("GetWorkspace");

            apiGroup.MapPatch("/workspaces/{workspaceId:guid}", WorkspacesHandlers.PatchWorkspace)
                .WithBodyValidation<PostWorkspaceRequest>()
                .Produces<WorkspaceResponse>()
                .WithName("UpdateWorkspace");

            apiGroup.MapDelete("/workspaces/{workspaceId:guid}", WorkspacesHandlers.DeleteWorkspace)
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteWorkspace");

            apiGroup.MapPost("/workspaces/{workspaceId:guid}/members", WorkspacesHandlers.PostWorkspacesUser)
                .WithBodyValidation<PostWorkspaceUserRequest>()
                .Produces<MemberListItem>()
                .WithName("CreateWorkspaceUser");

            apiGroup.MapPatch(
                    "/workspaces/{workspaceId:guid}/members/{userId:guid}",
                    WorkspacesHandlers.PatchWorkspaceUserRole
                )
                .WithBodyValidation<PatchWorkspaceUserRoleRequest>()
                .WithName("UpdateWorkspaceUserRole");

            apiGroup.MapDelete(
                    "/workspaces/{workspaceId:guid}/members/{userId:guid}",
                    WorkspacesHandlers.DeleteWorkspaceUser
                )
                .Produces(StatusCodes.Status200OK)
                .WithName("DeleteWorkspaceUser");
        }
    }
}
