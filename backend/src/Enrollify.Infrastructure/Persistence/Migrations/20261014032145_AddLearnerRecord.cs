using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrollify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_LRN",
                table: "Students");

            migrationBuilder.AlterColumn<string>(
                name: "LRN",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "AddressBarangayCode",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressBarangayName",
                table: "Students",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressCityMunicipalityCode",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressCityMunicipalityName",
                table: "Students",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressProvinceCode",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressProvinceName",
                table: "Students",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressRegionCode",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressRegionName",
                table: "Students",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressZipCode",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityTypes",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtensionName",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherContactNumber",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherFirstName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherLastName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherMiddleName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FourPsHouseholdId",
                table: "Students",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDisability",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IpCommunity",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Is4PsBeneficiary",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndigenousPeople",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LrnNeedsReview",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MotherContactNumber",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMaidenFirstName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMaidenLastName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMaidenMiddleName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherTongue",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfBirth",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PsaBirthCertificateNumber",
                table: "Students",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentNumber",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "KeepOnFile",
                table: "RequirementTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastGradeLevelCompleted",
                table: "Enrollments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSchoolAddress",
                table: "Enrollments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSchoolAttended",
                table: "Enrollments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSchoolId",
                table: "Enrollments",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSchoolYearCompleted",
                table: "Enrollments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LearnerType",
                table: "Enrollments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strand",
                table: "Enrollments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Track",
                table: "Enrollments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressBarangayCode",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressBarangayName",
                table: "AdmissionApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressCityMunicipalityCode",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressCityMunicipalityName",
                table: "AdmissionApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressProvinceCode",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressProvinceName",
                table: "AdmissionApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressRegionCode",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressRegionName",
                table: "AdmissionApplications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressZipCode",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityTypes",
                table: "AdmissionApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtensionName",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherContactNumber",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherFirstName",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherLastName",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherMiddleName",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FourPsHouseholdId",
                table: "AdmissionApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDisability",
                table: "AdmissionApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IpCommunity",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Is4PsBeneficiary",
                table: "AdmissionApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndigenousPeople",
                table: "AdmissionApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastGradeLevelCompleted",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSchoolId",
                table: "AdmissionApplications",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSchoolYearCompleted",
                table: "AdmissionApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LearnerType",
                table: "AdmissionApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lrn",
                table: "AdmissionApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherContactNumber",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMaidenFirstName",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMaidenLastName",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMaidenMiddleName",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherTongue",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfBirth",
                table: "AdmissionApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PsaBirthCertificateNumber",
                table: "AdmissionApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strand",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Track",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShsStrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Track = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Strand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShsStrands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_LRN",
                table: "Students",
                columns: new[] { "TenantId", "LRN" },
                unique: true,
                filter: "[LRN] IS NOT NULL");

            // Backfill Enrollify student numbers ("YYYY-NNNNN", Manila year, per tenant per year)
            // before the unique index exists. Same shape StudentNumbering mints at runtime.
            migrationBuilder.Sql(@"
WITH numbered AS (
    SELECT Id,
           YEAR(DATEADD(hour, 8, CreatedAt)) AS Yr,
           ROW_NUMBER() OVER (PARTITION BY TenantId, YEAR(DATEADD(hour, 8, CreatedAt)) ORDER BY CreatedAt, Id) AS Seq
    FROM Students
)
UPDATE s SET StudentNumber = CAST(n.Yr AS varchar(4)) + '-' + RIGHT('00000' + CAST(n.Seq AS varchar(10)), 5)
FROM Students s JOIN numbered n ON n.Id = s.Id;");

            // Every pre-existing LRN is either Enrollify-minted or unverified, and minted values
            // can't be told apart from real ones — flag them all for registrar review. Runs ONCE
            // (here), never at startup, so confirmed LRNs are never re-flagged.
            migrationBuilder.Sql("UPDATE Students SET LrnNeedsReview = 1 WHERE LRN IS NOT NULL;");

            // DepEd Order 17 s. 2025: the PSA birth certificate is submitted once per learner.
            migrationBuilder.Sql(@"
UPDATE RequirementTemplates SET KeepOnFile = 1
WHERE DocumentName LIKE '%PSA%' OR DocumentName LIKE '%Birth Certificate%';");

            migrationBuilder.CreateIndex(
                name: "IX_ShsStrands_TenantId_IsActive_DisplayOrder",
                table: "ShsStrands",
                columns: new[] { "TenantId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ShsStrands_TenantId_Track_Strand",
                table: "ShsStrands",
                columns: new[] { "TenantId", "Track", "Strand" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_StudentNumber",
                table: "Students",
                columns: new[] { "TenantId", "StudentNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema needs a non-null, unique LRN. Students without one take their student
            // number (contains '-', so it can't collide with a 12-digit LRN) — before it is dropped.
            migrationBuilder.Sql("UPDATE Students SET LRN = StudentNumber WHERE LRN IS NULL;");

            migrationBuilder.DropTable(
                name: "ShsStrands");

            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_LRN",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_StudentNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressBarangayCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressBarangayName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressCityMunicipalityCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressCityMunicipalityName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressProvinceCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressProvinceName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressRegionCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressRegionName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressZipCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DisabilityTypes",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExtensionName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherContactNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherFirstName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherLastName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherMiddleName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FourPsHouseholdId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "HasDisability",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IpCommunity",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Is4PsBeneficiary",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsIndigenousPeople",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "LrnNeedsReview",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherContactNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherMaidenFirstName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherMaidenLastName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherMaidenMiddleName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherTongue",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PlaceOfBirth",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PsaBirthCertificateNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "KeepOnFile",
                table: "RequirementTemplates");

            migrationBuilder.DropColumn(
                name: "LastGradeLevelCompleted",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastSchoolAddress",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastSchoolAttended",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastSchoolId",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastSchoolYearCompleted",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LearnerType",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Strand",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Track",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "AddressBarangayCode",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressBarangayName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressCityMunicipalityCode",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressCityMunicipalityName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressProvinceCode",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressProvinceName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressRegionCode",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressRegionName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "AddressZipCode",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DisabilityTypes",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExtensionName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FatherContactNumber",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FatherFirstName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FatherLastName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FatherMiddleName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FourPsHouseholdId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HasDisability",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "IpCommunity",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Is4PsBeneficiary",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "IsIndigenousPeople",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "LastGradeLevelCompleted",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "LastSchoolId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "LastSchoolYearCompleted",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "LearnerType",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Lrn",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "MotherContactNumber",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "MotherMaidenFirstName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "MotherMaidenLastName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "MotherMaidenMiddleName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "MotherTongue",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "PlaceOfBirth",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "PsaBirthCertificateNumber",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Strand",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Track",
                table: "AdmissionApplications");

            migrationBuilder.AlterColumn<string>(
                name: "LRN",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_LRN",
                table: "Students",
                columns: new[] { "TenantId", "LRN" },
                unique: true);
        }
    }
}
