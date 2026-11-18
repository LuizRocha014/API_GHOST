using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaAvaliacaoSerie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "AgendaBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "AgendaBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "AgendaBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgendaBookings_SeriesId",
                table: "AgendaBookings",
                column: "SeriesId",
                filter: "[SeriesId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgendaBookings_SeriesId",
                table: "AgendaBookings");

            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "AgendaBookings");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "AgendaBookings");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "AgendaBookings");
        }
    }
}
