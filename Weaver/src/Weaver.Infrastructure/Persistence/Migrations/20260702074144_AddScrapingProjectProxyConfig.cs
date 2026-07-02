using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapingProjectProxyConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProxyConfigJson",
                table: "scraping_projects",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProxyConfigJson",
                table: "scraping_projects");
        }
    }
}
