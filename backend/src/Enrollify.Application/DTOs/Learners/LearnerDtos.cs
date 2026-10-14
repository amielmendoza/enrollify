namespace Enrollify.Application.DTOs.Learners;

/// <summary>PSGC-resolved address. Codes are 10-digit strings; names travel with them.</summary>
public record PsgcAddressDto(
    string? RegionCode = null, string? RegionName = null,
    string? ProvinceCode = null, string? ProvinceName = null,
    string? CityMunicipalityCode = null, string? CityMunicipalityName = null,
    string? BarangayCode = null, string? BarangayName = null,
    string? ZipCode = null);

/// <summary>DepEd BEEF learner fields (no LRN — that travels separately, with its own rules).</summary>
public record LearnerProfileDto(
    string? ExtensionName = null,
    string? PsaBirthCertificateNumber = null,
    string? PlaceOfBirth = null,
    string? MotherTongue = null,
    string? Religion = null,
    bool IsIndigenousPeople = false,
    string? IpCommunity = null,
    bool Is4PsBeneficiary = false,
    string? FourPsHouseholdId = null,
    bool HasDisability = false,
    List<string>? DisabilityTypes = null,
    string? FatherLastName = null,
    string? FatherFirstName = null,
    string? FatherMiddleName = null,
    string? FatherContactNumber = null,
    string? MotherMaidenLastName = null,
    string? MotherMaidenFirstName = null,
    string? MotherMaidenMiddleName = null,
    string? MotherContactNumber = null,
    PsgcAddressDto? HomeAddress = null);

/// <summary>Per-enrollment DepEd fields (learner type, previous school, SHS track/strand).</summary>
public record EnrollmentLearnerInfoDto(
    string? LearnerType = null,
    string? LastGradeLevelCompleted = null,
    string? LastSchoolYearCompleted = null,
    string? LastSchoolAttended = null,
    string? LastSchoolAddress = null,
    string? LastSchoolId = null,
    string? Track = null,
    string? Strand = null);
