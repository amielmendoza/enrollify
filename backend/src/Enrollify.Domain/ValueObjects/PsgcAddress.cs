namespace Enrollify.Domain.ValueObjects;

/// <summary>
/// A Philippine address resolved against the PSA's Philippine Standard Geographic Code (PSGC).
/// Codes AND names are stored so a later PSGC revision never rewrites how an old record reads.
/// Codes are 10-digit strings (leading zeros matter — never numbers). ProvinceCode is null for
/// cities that sit directly under a region (NCR, independent/highly-urbanized cities).
/// Owned by Student and AdmissionApplication; the columns live in the owner's table.
/// </summary>
public class PsgcAddress
{
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? ProvinceCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityMunicipalityCode { get; set; }
    public string? CityMunicipalityName { get; set; }
    public string? BarangayCode { get; set; }
    public string? BarangayName { get; set; }
    public string? ZipCode { get; set; }

    /// <summary>True once the address is resolved down to a barangay (what SF1 needs).</summary>
    public bool IsMatched => CityMunicipalityCode != null && BarangayCode != null;
}
