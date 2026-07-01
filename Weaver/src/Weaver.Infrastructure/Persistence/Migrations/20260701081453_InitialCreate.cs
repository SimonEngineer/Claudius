using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rate_limit_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyScope = table.Column<int>(type: "integer", nullable: false),
                    CustomKeyTemplate = table.Column<string>(type: "text", nullable: true),
                    PermitLimit = table.Column<int>(type: "integer", nullable: false),
                    WindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    BurstCapacity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_limit_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scrape_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScrapingProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    StreamMessageId = table.Column<string>(type: "text", nullable: true),
                    LeaseOwner = table.Column<string>(type: "text", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ScrapeRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scraping_projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartUrl = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ItemSelector = table.Column<string>(type: "text", nullable: true),
                    PaginationStrategy = table.Column<int>(type: "integer", nullable: false),
                    NextPageSelector = table.Column<string>(type: "text", nullable: true),
                    PageUrlTemplate = table.Column<string>(type: "text", nullable: true),
                    MaxPages = table.Column<int>(type: "integer", nullable: false),
                    RateLimitPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scraping_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scraping_projects_rate_limit_policies_RateLimitPolicyId",
                        column: x => x.RateLimitPolicyId,
                        principalTable: "rate_limit_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workflow_edges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceHandle = table.Column<string>(type: "text", nullable: true),
                    TargetNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetHandle = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_edges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_edges_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    PositionX = table.Column<double>(type: "double precision", nullable: false),
                    PositionY = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_nodes_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    TriggerNodeType = table.Column<string>(type: "text", nullable: true),
                    TriggerPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_runs_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "field_selectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScrapingProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Selector = table.Column<string>(type: "text", nullable: false),
                    Attribute = table.Column<int>(type: "integer", nullable: false),
                    AttributeName = table.Column<string>(type: "text", nullable: true),
                    ResolveUrl = table.Column<bool>(type: "boolean", nullable: false),
                    IsKey = table.Column<bool>(type: "boolean", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_selectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_field_selectors_scraping_projects_ScrapingProjectId",
                        column: x => x.ScrapingProjectId,
                        principalTable: "scraping_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scrape_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScrapingProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    PagesCrawled = table.Column<int>(type: "integer", nullable: false),
                    ItemsFound = table.Column<int>(type: "integer", nullable: false),
                    ItemsChanged = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scrape_runs_scraping_projects_ScrapingProjectId",
                        column: x => x.ScrapingProjectId,
                        principalTable: "scraping_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "node_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: false),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    LogText = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_node_runs_workflow_runs_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "workflow_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scraped_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScrapeRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScrapingProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    ItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scraped_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scraped_items_scrape_runs_ScrapeRunId",
                        column: x => x.ScrapeRunId,
                        principalTable: "scrape_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_field_selectors_ScrapingProjectId_Name",
                table: "field_selectors",
                columns: new[] { "ScrapingProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_node_runs_WorkflowRunId",
                table: "node_runs",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_scrape_jobs_Status",
                table: "scrape_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_scrape_runs_ScrapingProjectId",
                table: "scrape_runs",
                column: "ScrapingProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_scraped_items_ScrapeRunId",
                table: "scraped_items",
                column: "ScrapeRunId");

            migrationBuilder.CreateIndex(
                name: "IX_scraped_items_ScrapingProjectId_ItemKey",
                table: "scraped_items",
                columns: new[] { "ScrapingProjectId", "ItemKey" });

            migrationBuilder.CreateIndex(
                name: "IX_scraping_projects_RateLimitPolicyId",
                table: "scraping_projects",
                column: "RateLimitPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_edges_SourceNodeId",
                table: "workflow_edges",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_edges_TargetNodeId",
                table: "workflow_edges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_edges_WorkflowId",
                table: "workflow_edges",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_nodes_WorkflowId_Type",
                table: "workflow_nodes",
                columns: new[] { "WorkflowId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_WorkflowId",
                table: "workflow_runs",
                column: "WorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "field_selectors");

            migrationBuilder.DropTable(
                name: "node_runs");

            migrationBuilder.DropTable(
                name: "scrape_jobs");

            migrationBuilder.DropTable(
                name: "scraped_items");

            migrationBuilder.DropTable(
                name: "workflow_edges");

            migrationBuilder.DropTable(
                name: "workflow_nodes");

            migrationBuilder.DropTable(
                name: "workflow_runs");

            migrationBuilder.DropTable(
                name: "scrape_runs");

            migrationBuilder.DropTable(
                name: "workflows");

            migrationBuilder.DropTable(
                name: "scraping_projects");

            migrationBuilder.DropTable(
                name: "rate_limit_policies");
        }
    }
}
