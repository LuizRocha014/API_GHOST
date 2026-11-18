using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaBotFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SeenAt",
                table: "BotSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "BotMessages",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meta",
                table: "BotMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BotBlocked",
                table: "AgendaClients",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeenAt",
                table: "BotSessions");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "BotMessages");

            migrationBuilder.DropColumn(
                name: "Meta",
                table: "BotMessages");

            migrationBuilder.DropColumn(
                name: "BotBlocked",
                table: "AgendaClients");
        }
    }
}
