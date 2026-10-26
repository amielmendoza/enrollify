namespace Enrollify.Application.DTOs.SchoolProfile;

/// <summary>
/// The school's DepEd identity as printed in report headers. <see cref="SchoolName"/> is the
/// tenant name (owned by SuperAdmin, read-only here).
/// </summary>
public record SchoolProfileDto(
    string SchoolName,
    string? DepEdSchoolId,
    string? DepEdRegionCode,
    string? DepEdRegionName,
    string? DepEdDivision,
    string? DepEdDistrict,
    string? SchoolHeadName);

public record UpdateSchoolProfileRequest(
    string? DepEdSchoolId,
    string? DepEdRegionCode,
    string? DepEdRegionName,
    string? DepEdDivision,
    string? DepEdDistrict,
    string? SchoolHeadName);
