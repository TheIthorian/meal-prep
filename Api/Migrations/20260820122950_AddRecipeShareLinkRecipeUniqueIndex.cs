using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeShareLinkRecipeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeShareLinks_RecipeId",
                table: "RecipeShareLinks");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeShareLinks_RecipeId",
                table: "RecipeShareLinks",
                column: "RecipeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeShareLinks_RecipeId",
                table: "RecipeShareLinks");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeShareLinks_RecipeId",
                table: "RecipeShareLinks",
                column: "RecipeId");
        }
    }
}
