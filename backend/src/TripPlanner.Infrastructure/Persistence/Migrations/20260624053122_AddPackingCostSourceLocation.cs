using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackingCostSourceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceLocationId",
                table: "TripStops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Bookings",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackingItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsPacked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackingItems_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackingItems_TripId",
                table: "PackingItems",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackingItems");

            migrationBuilder.DropColumn(
                name: "SourceLocationId",
                table: "TripStops");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Bookings");
        }
    }
}
