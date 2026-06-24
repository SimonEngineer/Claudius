using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NameTags.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMountingHoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TagMountingHoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    OffsetXMm = table.Column<float>(type: "REAL", nullable: false),
                    OffsetYMm = table.Column<float>(type: "REAL", nullable: false),
                    DiameterMm = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagMountingHoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagMountingHoles_TagProjects_TagProjectId",
                        column: x => x.TagProjectId,
                        principalTable: "TagProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagMountingHoles_TagProjectId",
                table: "TagMountingHoles",
                column: "TagProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagMountingHoles");
        }
    }
}
