using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthRenderModeAndRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "workflows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "workflow_nodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryDelayMs",
                table: "workflow_nodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "scraping_projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "RenderMode",
                table: "scraping_projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "rate_limit_policies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_OwnerUserId",
                table: "workflows",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_scraping_projects_OwnerUserId",
                table: "scraping_projects",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rate_limit_policies_OwnerUserId",
                table: "rate_limit_policies",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_workflows_OwnerUserId",
                table: "workflows");

            migrationBuilder.DropIndex(
                name: "IX_scraping_projects_OwnerUserId",
                table: "scraping_projects");

            migrationBuilder.DropIndex(
                name: "IX_rate_limit_policies_OwnerUserId",
                table: "rate_limit_policies");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "workflow_nodes");

            migrationBuilder.DropColumn(
                name: "RetryDelayMs",
                table: "workflow_nodes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "scraping_projects");

            migrationBuilder.DropColumn(
                name: "RenderMode",
                table: "scraping_projects");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "rate_limit_policies");
        }
    }
}
