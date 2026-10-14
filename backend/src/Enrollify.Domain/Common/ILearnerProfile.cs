using Enrollify.Domain.ValueObjects;

namespace Enrollify.Domain.Common;

/// <summary>
/// DepEd Basic Education Enrollment Form learner fields, shared by Student (the record of truth)
/// and AdmissionApplication (what the family submitted) so mapping and validation exist once.
/// </summary>
public interface ILearnerProfile
{
    string? ExtensionName { get; set; }
    string? PsaBirthCertificateNumber { get; set; }
    string? PlaceOfBirth { get; set; }
    string? MotherTongue { get; set; }
    string? Religion { get; set; }
    bool IsIndigenousPeople { get; set; }
    string? IpCommunity { get; set; }
    bool Is4PsBeneficiary { get; set; }
    string? FourPsHouseholdId { get; set; }
    bool HasDisability { get; set; }
    /// <summary>Comma-separated values from DepEdLists.DisabilityTypes (none contain commas).</summary>
    string? DisabilityTypes { get; set; }
    string? FatherLastName { get; set; }
    string? FatherFirstName { get; set; }
    string? FatherMiddleName { get; set; }
    string? FatherContactNumber { get; set; }
    string? MotherMaidenLastName { get; set; }
    string? MotherMaidenFirstName { get; set; }
    string? MotherMaidenMiddleName { get; set; }
    string? MotherContactNumber { get; set; }
    PsgcAddress HomeAddress { get; set; }
}
