using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaFidelidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientPlanId",
                table: "AgendaBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgendaLoyaltyPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaLoyaltyPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaLoyaltyPlans_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgendaClientPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FullPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaClientPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaClientPlans_AgendaClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "AgendaClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgendaClientPlans_AgendaLoyaltyPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "AgendaLoyaltyPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaBookings_ClientPlanId_Date",
                table: "AgendaBookings",
                columns: new[] { "ClientPlanId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaClientPlans_BranchId_ClientId",
                table: "AgendaClientPlans",
                columns: new[] { "BranchId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaClientPlans_ClientId",
                table: "AgendaClientPlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaClientPlans_PlanId",
                table: "AgendaClientPlans",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaLoyaltyPlans_BranchId",
                table: "AgendaLoyaltyPlans",
                column: "BranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendaClientPlans");

            migrationBuilder.DropTable(
                name: "AgendaLoyaltyPlans");

            migrationBuilder.DropIndex(
                name: "IX_AgendaBookings_ClientPlanId_Date",
                table: "AgendaBookings");

            migrationBuilder.DropColumn(
                name: "ClientPlanId",
                table: "AgendaBookings");
        }
    }
}
