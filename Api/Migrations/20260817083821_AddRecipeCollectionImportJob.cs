using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeCollectionImportJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeCollectionImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceCollectionName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetRecipeCollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeCollectionImportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeCollectionImportJobs_AspNetUsers_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeCollectionImportJobs_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeCollectionImportJobItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeCollectionImportJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RecipeTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImportedRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeCollectionImportJobItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeCollectionImportJobItems_RecipeCollectionImportJobs_R~",
                        column: x => x.RecipeCollectionImportJobId,
                        principalTable: "RecipeCollectionImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeCollectionImportJobItems_RecipeCollectionImportJobId_~",
                table: "RecipeCollectionImportJobItems",
                columns: new[] { "RecipeCollectionImportJobId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeCollectionImportJobs_StartedByUserId",
                table: "RecipeCollectionImportJobs",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeCollectionImportJobs_WorkspaceId_ShareToken",
                table: "RecipeCollectionImportJobs",
                columns: new[] { "WorkspaceId", "ShareToken" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeCollectionImportJobs_WorkspaceId_Status",
                table: "RecipeCollectionImportJobs",
                columns: new[] { "WorkspaceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeCollectionImportJobItems");

            migrationBuilder.DropTable(
                name: "RecipeCollectionImportJobs");
        }
    }
}
