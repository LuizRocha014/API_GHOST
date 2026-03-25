using System;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessesAndUserBranchAccessLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultAccessId = SeedIds.DefaultAccessId;
            var seedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.CreateTable(
                name: "Accesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accesses", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Accesses",
                columns: new[] { "Id", "Name", "Code", "CreatedAt", "UpdatedAt", "Active" },
                values: new object[] { defaultAccessId, "Geral", "GERAL", seedTime, seedTime, true });

            migrationBuilder.DropIndex(
                name: "IX_UserCompanyBranches_UserId_BranchId",
                table: "UserCompanyBranches");

            migrationBuilder.AddColumn<Guid>(
                name: "AccessId",
                table: "UserCompanyBranches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultAccessId);

            migrationBuilder.CreateIndex(
                name: "IX_Accesses_Code",
                table: "Accesses",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyBranches_AccessId",
                table: "UserCompanyBranches",
                column: "AccessId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyBranches_UserId_BranchId_AccessId",
                table: "UserCompanyBranches",
                columns: new[] { "UserId", "BranchId", "AccessId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCompanyBranches_Accesses_AccessId",
                table: "UserCompanyBranches",
                column: "AccessId",
                principalTable: "Accesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCompanyBranches_Accesses_AccessId",
                table: "UserCompanyBranches");

            migrationBuilder.DropTable(
                name: "Accesses");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanyBranches_AccessId",
                table: "UserCompanyBranches");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanyBranches_UserId_BranchId_AccessId",
                table: "UserCompanyBranches");

            migrationBuilder.DropColumn(
                name: "AccessId",
                table: "UserCompanyBranches");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyBranches_UserId_BranchId",
                table: "UserCompanyBranches",
                columns: new[] { "UserId", "BranchId" },
                unique: true);
        }
    }
}
