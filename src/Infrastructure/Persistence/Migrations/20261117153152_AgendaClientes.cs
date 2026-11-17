using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "AgendaClients",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Vip",
                table: "AgendaClients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "AgendaBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "AgendaBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgendaClientVehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vehicle = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaClientVehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaClientVehicles_AgendaClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "AgendaClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaBookings_BranchId_ClientId",
                table: "AgendaBookings",
                columns: new[] { "BranchId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaClientVehicles_BranchId_ClientId",
                table: "AgendaClientVehicles",
                columns: new[] { "BranchId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaClientVehicles_ClientId",
                table: "AgendaClientVehicles",
                column: "ClientId");

            // Agendamentos de antes do cadastro de clientes: liga ao cliente pelo telefone e cria os carros pelas placas.
            migrationBuilder.Sql("""
                UPDATE b SET b.ClientId = c.Id
                  FROM AgendaBookings b
                  JOIN AgendaClients c ON c.BranchId = b.BranchId AND c.Phone = b.Phone AND c.Active = 1
                 WHERE b.Phone <> '' AND b.ClientId IS NULL;
                """);
            migrationBuilder.Sql("""
                INSERT INTO AgendaClientVehicles (Id, BranchId, ClientId, Vehicle, Plate, VehicleType, Color, CreatedAt, UpdatedAt, Active)
                SELECT NEWID(), x.BranchId, x.ClientId, x.Vehicle, x.Plate, x.VehicleType, '', x.CreatedAt, x.CreatedAt, 1
                  FROM (SELECT BranchId, ClientId, Plate, Vehicle, VehicleType, CreatedAt,
                               ROW_NUMBER() OVER (PARTITION BY BranchId, ClientId, Plate ORDER BY [Date] DESC, StartMin DESC) AS rn
                          FROM AgendaBookings
                         WHERE ClientId IS NOT NULL AND Plate <> '' AND Active = 1) x
                 WHERE x.rn = 1;
                """);
            migrationBuilder.Sql("""
                UPDATE b SET b.VehicleId = v.Id
                  FROM AgendaBookings b
                  JOIN AgendaClientVehicles v ON v.ClientId = b.ClientId AND v.Plate = b.Plate AND v.Active = 1
                 WHERE b.Plate <> '' AND b.VehicleId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendaClientVehicles");

            migrationBuilder.DropIndex(
                name: "IX_AgendaBookings_BranchId_ClientId",
                table: "AgendaBookings");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "AgendaClients");

            migrationBuilder.DropColumn(
                name: "Vip",
                table: "AgendaClients");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "AgendaBookings");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "AgendaBookings");
        }
    }
}
