using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrollify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GradeLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SchoolYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviousSchool = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreviousSchoolAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GuardianName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuardianContact = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GuardianRelationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_TenantId_ApplicationNumber",
                table: "AdmissionApplications",
                columns: new[] { "TenantId", "ApplicationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_TenantId_Status",
                table: "AdmissionApplications",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionApplications");
        }
    }
}
