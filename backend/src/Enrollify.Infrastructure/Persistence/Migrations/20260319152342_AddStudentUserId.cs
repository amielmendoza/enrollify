using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrollify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Students",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_UserId",
                table: "Students",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_UserId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Students");
        }
    }
}
