using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NameTags.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTextMarginsAndAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextHorizontalAlign",
                table: "TagProjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "TextMarginBottomMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "TextMarginLeftMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "TextMarginRightMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "TextMarginTopMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "TextVerticalAlign",
                table: "TagProjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextHorizontalAlign",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "TextMarginBottomMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "TextMarginLeftMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "TextMarginRightMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "TextMarginTopMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "TextVerticalAlign",
                table: "TagProjects");
        }
    }
}
