using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectModelColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LocalModel",
                table: "Projects",
                newName: "WorkerModel");

            migrationBuilder.RenameColumn(
                name: "CloudModel",
                table: "Projects",
                newName: "SupervisorModel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WorkerModel",
                table: "Projects",
                newName: "LocalModel");

            migrationBuilder.RenameColumn(
                name: "SupervisorModel",
                table: "Projects",
                newName: "CloudModel");
        }
    }
}
