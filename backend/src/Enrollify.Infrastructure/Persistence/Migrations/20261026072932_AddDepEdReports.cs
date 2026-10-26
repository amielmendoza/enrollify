using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrollify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepEdReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepEdDistrict",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepEdDivision",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepEdRegionCode",
                table: "Tenants",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepEdRegionName",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepEdSchoolId",
                table: "Tenants",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolHeadName",
                table: "Tenants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstAttendanceDate",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeater",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LisEncodedAt",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LisEncodedBy",
                table: "Enrollments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferredToSchool",
                table: "Enrollments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawalDate",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalReason",
                table: "Enrollments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawalType",
                table: "Enrollments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // One-time backfill: existing students take their guardian relationship from their most
            // recently updated Approved application that has a non-blank value (the startup
            // GuardianRelationshipBackfill does the same for anything left over, idempotently).
            migrationBuilder.Sql(@"
UPDATE s SET GuardianRelationship = a.GuardianRelationship
FROM Students s
CROSS APPLY (
    SELECT TOP 1 GuardianRelationship FROM AdmissionApplications a
    WHERE a.StudentId = s.Id AND a.Status = 'Approved'
      AND a.GuardianRelationship IS NOT NULL AND LTRIM(RTRIM(a.GuardianRelationship)) <> ''
    ORDER BY a.UpdatedAt DESC, a.CreatedAt DESC
) a
WHERE s.GuardianRelationship IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepEdDistrict",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DepEdDivision",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DepEdRegionCode",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DepEdRegionName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DepEdSchoolId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SchoolHeadName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FirstAttendanceDate",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "IsRepeater",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LisEncodedAt",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LisEncodedBy",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "TransferredToSchool",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "WithdrawalDate",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "WithdrawalReason",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "WithdrawalType",
                table: "Enrollments");
        }
    }
}
