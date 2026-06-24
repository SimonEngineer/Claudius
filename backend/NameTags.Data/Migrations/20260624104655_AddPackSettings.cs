using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NameTags.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "CharmHookBandThicknessMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "CharmHookOuterRadiusMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "CharmPlateHeightMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "CharmPlateWidthMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ClipArmLengthMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ClipArmThicknessMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ClipGapMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ClipPlateHeightMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "ClipPlateWidthMm",
                table: "TagProjects",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CharmHookBandThicknessMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "CharmHookOuterRadiusMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "CharmPlateHeightMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "CharmPlateWidthMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "ClipArmLengthMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "ClipArmThicknessMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "ClipGapMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "ClipPlateHeightMm",
                table: "TagProjects");

            migrationBuilder.DropColumn(
                name: "ClipPlateWidthMm",
                table: "TagProjects");
        }
    }
}
