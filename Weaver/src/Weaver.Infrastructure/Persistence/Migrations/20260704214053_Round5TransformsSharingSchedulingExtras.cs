using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Round5TransformsSharingSchedulingExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TriggerNodeId",
                table: "workflow_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CrawlDelayMs",
                table: "scraping_projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShareTokenHash",
                table: "scraping_projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SitemapUrl",
                table: "scraping_projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "scraping_projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransformsJson",
                table: "field_selectors",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerNodeId",
                table: "workflow_runs");

            migrationBuilder.DropColumn(
                name: "CrawlDelayMs",
                table: "scraping_projects");

            migrationBuilder.DropColumn(
                name: "ShareTokenHash",
                table: "scraping_projects");

            migrationBuilder.DropColumn(
                name: "SitemapUrl",
                table: "scraping_projects");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "scraping_projects");

            migrationBuilder.DropColumn(
                name: "TransformsJson",
                table: "field_selectors");
        }
    }
}
