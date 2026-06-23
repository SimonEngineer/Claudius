using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NameTags.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TagProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ShapeType = table.Column<string>(type: "TEXT", nullable: false),
                    CornerRadiusMm = table.Column<float>(type: "REAL", nullable: false),
                    StarPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    StarInnerRadiusRatio = table.Column<float>(type: "REAL", nullable: false),
                    CurveSegments = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomSvgBytes = table.Column<byte[]>(type: "BLOB", nullable: true),
                    FontFamilyOrPath = table.Column<string>(type: "TEXT", nullable: false),
                    PlateWidthMm = table.Column<float>(type: "REAL", nullable: false),
                    PlateHeightMm = table.Column<float>(type: "REAL", nullable: false),
                    PlateThicknessMm = table.Column<float>(type: "REAL", nullable: false),
                    TextDepthMm = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagNames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagNames_TagProjects_TagProjectId",
                        column: x => x.TagProjectId,
                        principalTable: "TagProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagNames_TagProjectId_SortOrder",
                table: "TagNames",
                columns: new[] { "TagProjectId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagNames");

            migrationBuilder.DropTable(
                name: "TagProjects");
        }
    }
}
