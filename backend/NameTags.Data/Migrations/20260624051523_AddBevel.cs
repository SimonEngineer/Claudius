using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NameTags.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BevelMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BevelMm",
                table: "TagProjects");
        }
    }
}
