using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrollify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParentChildModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_UserId",
                table: "Students");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Students",
                newName: "ParentUserId");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentUserId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_ParentUserId",
                table: "Students",
                columns: new[] { "TenantId", "ParentUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_TenantId_ParentUserId",
                table: "AdmissionApplications",
                columns: new[] { "TenantId", "ParentUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_ParentUserId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionApplications_TenantId_ParentUserId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ParentUserId",
                table: "AdmissionApplications");

            migrationBuilder.RenameColumn(
                name: "ParentUserId",
                table: "Students",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_UserId",
                table: "Students",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }
    }
}
