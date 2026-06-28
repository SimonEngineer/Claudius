using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NameTags.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNameTextDepthOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "TextDepthMmOverride",
                table: "TagNames",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextDepthMmOverride",
                table: "TagNames");
        }
    }
}
