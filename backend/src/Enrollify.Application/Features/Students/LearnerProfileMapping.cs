using Enrollify.Application.DTOs.Learners;
using Enrollify.Domain.Common;
using Enrollify.Domain.ValueObjects;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// The one place DepEd learner fields move between DTOs and entities (Student, AdmissionApplication).
/// Follow-up fields are cleared whenever their yes/no is off, so stale values never linger.
/// The owned PsgcAddress is always mutated in place (never replaced) so EF tracks it as an update.
/// </summary>
public static class LearnerProfileMapping
{
    public static LearnerProfileDto ToDto(ILearnerProfile p) => new(
        p.ExtensionName, p.PsaBirthCertificateNumber, p.PlaceOfBirth, p.MotherTongue, p.Religion,
        p.IsIndigenousPeople, p.IpCommunity, p.Is4PsBeneficiary, p.FourPsHouseholdId,
        p.HasDisability, SplitDisabilities(p.DisabilityTypes),
        p.FatherLastName, p.FatherFirstName, p.FatherMiddleName, p.FatherContactNumber,
        p.MotherMaidenLastName, p.MotherMaidenFirstName, p.MotherMaidenMiddleName, p.MotherContactNumber,
        ToDto(p.HomeAddress));

    public static PsgcAddressDto ToDto(PsgcAddress? a) => a == null
        ? new PsgcAddressDto()
        : new PsgcAddressDto(a.RegionCode, a.RegionName, a.ProvinceCode, a.ProvinceName,
            a.CityMunicipalityCode, a.CityMunicipalityName, a.BarangayCode, a.BarangayName, a.ZipCode);

    /// <summary>
    /// Overwrites every learner field on <paramref name="target"/> (staff edit, new application, new student).
    /// A null HomeAddress means "unchanged" — a new entity's address is already empty.
    /// </summary>
    public static void Apply(LearnerProfileDto dto, ILearnerProfile target)
    {
        target.ExtensionName = Clean(dto.ExtensionName);
        target.PsaBirthCertificateNumber = Clean(dto.PsaBirthCertificateNumber);
        target.PlaceOfBirth = Clean(dto.PlaceOfBirth);
        target.MotherTongue = Clean(dto.MotherTongue);
        target.Religion = Clean(dto.Religion);
        target.IsIndigenousPeople = dto.IsIndigenousPeople;
        target.IpCommunity = dto.IsIndigenousPeople ? Clean(dto.IpCommunity) : null;
        target.Is4PsBeneficiary = dto.Is4PsBeneficiary;
        target.FourPsHouseholdId = dto.Is4PsBeneficiary ? Clean(dto.FourPsHouseholdId) : null;
        target.HasDisability = dto.HasDisability;
        target.DisabilityTypes = dto.HasDisability ? JoinDisabilities(dto.DisabilityTypes) : null;
        target.FatherLastName = Clean(dto.FatherLastName);
        target.FatherFirstName = Clean(dto.FatherFirstName);
        target.FatherMiddleName = Clean(dto.FatherMiddleName);
        target.FatherContactNumber = Clean(dto.FatherContactNumber);
        target.MotherMaidenLastName = Clean(dto.MotherMaidenLastName);
        target.MotherMaidenFirstName = Clean(dto.MotherMaidenFirstName);
        target.MotherMaidenMiddleName = Clean(dto.MotherMaidenMiddleName);
        target.MotherContactNumber = Clean(dto.MotherContactNumber);
        if (dto.HomeAddress != null)
            ApplyAddress(dto.HomeAddress, target.HomeAddress);
    }

    /// <summary>Overwrites the address in place; a null DTO clears it. Names are dropped when their code is.</summary>
    public static void ApplyAddress(PsgcAddressDto? dto, PsgcAddress target)
    {
        target.RegionCode = Clean(dto?.RegionCode);
        target.RegionName = target.RegionCode == null ? null : Clean(dto?.RegionName);
        target.ProvinceCode = Clean(dto?.ProvinceCode);
        target.ProvinceName = target.ProvinceCode == null ? null : Clean(dto?.ProvinceName);
        target.CityMunicipalityCode = Clean(dto?.CityMunicipalityCode);
        target.CityMunicipalityName = target.CityMunicipalityCode == null ? null : Clean(dto?.CityMunicipalityName);
        target.BarangayCode = Clean(dto?.BarangayCode);
        target.BarangayName = target.BarangayCode == null ? null : Clean(dto?.BarangayName);
        target.ZipCode = Clean(dto?.ZipCode);
    }

    /// <summary>
    /// Fills only blank fields on <paramref name="target"/> from <paramref name="source"/> — used when
    /// approval matches an existing student. A false yes/no counts as blank; the address is copied only
    /// when the target's isn't PSGC-matched yet.
    /// </summary>
    public static void FillBlanks(ILearnerProfile source, ILearnerProfile target)
    {
        target.ExtensionName ??= source.ExtensionName;
        target.PsaBirthCertificateNumber ??= source.PsaBirthCertificateNumber;
        target.PlaceOfBirth ??= source.PlaceOfBirth;
        target.MotherTongue ??= source.MotherTongue;
        target.Religion ??= source.Religion;
        if (!target.IsIndigenousPeople && source.IsIndigenousPeople)
        {
            target.IsIndigenousPeople = true;
            target.IpCommunity ??= source.IpCommunity;
        }
        if (!target.Is4PsBeneficiary && source.Is4PsBeneficiary)
        {
            target.Is4PsBeneficiary = true;
            target.FourPsHouseholdId ??= source.FourPsHouseholdId;
        }
        if (!target.HasDisability && source.HasDisability)
        {
            target.HasDisability = true;
            target.DisabilityTypes ??= source.DisabilityTypes;
        }
        target.FatherLastName ??= source.FatherLastName;
        target.FatherFirstName ??= source.FatherFirstName;
        target.FatherMiddleName ??= source.FatherMiddleName;
        target.FatherContactNumber ??= source.FatherContactNumber;
        target.MotherMaidenLastName ??= source.MotherMaidenLastName;
        target.MotherMaidenFirstName ??= source.MotherMaidenFirstName;
        target.MotherMaidenMiddleName ??= source.MotherMaidenMiddleName;
        target.MotherContactNumber ??= source.MotherContactNumber;
        if (!target.HomeAddress.IsMatched && source.HomeAddress.IsMatched)
            ApplyAddress(ToDto(source.HomeAddress), target.HomeAddress);
    }

    public static string? JoinDisabilities(IEnumerable<string>? types)
    {
        var cleaned = (types ?? Enumerable.Empty<string>())
            .Select(t => t?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return cleaned.Count == 0 ? null : string.Join(",", cleaned);
    }

    public static List<string> SplitDisabilities(string? csv) =>
        csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? new List<string>();

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
