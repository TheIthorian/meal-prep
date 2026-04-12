using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AlterMcpPersonalAccessTokenWorkspaceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prior tokens had no workspace; drop them so we can add a non-null FK safely.
            migrationBuilder.Sql("""DELETE FROM "McpPersonalAccessTokens";""");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "McpPersonalAccessTokens",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_McpPersonalAccessTokens_WorkspaceId",
                table: "McpPersonalAccessTokens",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_McpPersonalAccessTokens_Workspaces_WorkspaceId",
                table: "McpPersonalAccessTokens",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_McpPersonalAccessTokens_Workspaces_WorkspaceId",
                table: "McpPersonalAccessTokens");

            migrationBuilder.DropIndex(
                name: "IX_McpPersonalAccessTokens_WorkspaceId",
                table: "McpPersonalAccessTokens");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "McpPersonalAccessTokens");
        }
    }
}
