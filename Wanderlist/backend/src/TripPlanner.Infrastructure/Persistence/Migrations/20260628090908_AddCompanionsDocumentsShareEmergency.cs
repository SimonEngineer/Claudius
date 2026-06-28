using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanionsDocumentsShareEmergency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmergencyInfo",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShareSlug",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByCompanionId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SplitCompanionIds",
                table: "Expenses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TravelDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    DocType = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelDocuments_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripCompanions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripCompanions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripCompanions_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ShareSlug",
                table: "Trips",
                column: "ShareSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelDocuments_TripId",
                table: "TravelDocuments",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripCompanions_TripId",
                table: "TripCompanions",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TravelDocuments");

            migrationBuilder.DropTable(
                name: "TripCompanions");

            migrationBuilder.DropIndex(
                name: "IX_Trips_ShareSlug",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EmergencyInfo",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ShareSlug",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PaidByCompanionId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SplitCompanionIds",
                table: "Expenses");
        }
    }
}
