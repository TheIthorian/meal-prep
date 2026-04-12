using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeImportAiLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeImportAiLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProviderBaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    ResponseContent = table.Column<string>(type: "text", nullable: true),
                    FinishReason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ParsedSuccessfully = table.Column<bool>(type: "boolean", nullable: false),
                    FailureDetail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeImportAiLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeImportAiLogs_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImportAiLogs_WorkspaceId_CreatedAt",
                table: "RecipeImportAiLogs",
                columns: new[] { "WorkspaceId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeImportAiLogs");
        }
    }
}
