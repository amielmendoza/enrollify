using Enrollify.Application.Common;
using Enrollify.Application.DTOs.Learners;
using FluentValidation;

namespace Enrollify.Application.Features.Students;

// Lengths mirror LearnerProfileConfiguration / EnrollmentConfiguration. Blank strings are
// treated as "not provided" everywhere (LearnerProfileMapping.Clean turns them into null).
// Digit checks use [0-9] / char.IsAsciiDigit — .NET's \d also matches non-ASCII digits.

public class PsgcAddressDtoValidator : AbstractValidator<PsgcAddressDto>
{
    public PsgcAddressDtoValidator()
    {
        RuleFor(x => x.RegionCode).Must(BeTenDigits).When(x => Has(x.RegionCode)).WithMessage("Region code must be a 10-digit PSGC code.");
        RuleFor(x => x.ProvinceCode).Must(BeTenDigits).When(x => Has(x.ProvinceCode)).WithMessage("Province code must be a 10-digit PSGC code.");
        RuleFor(x => x.CityMunicipalityCode).Must(BeTenDigits).When(x => Has(x.CityMunicipalityCode)).WithMessage("City/municipality code must be a 10-digit PSGC code.");
        RuleFor(x => x.BarangayCode).Must(BeTenDigits).When(x => Has(x.BarangayCode)).WithMessage("Barangay code must be a 10-digit PSGC code.");

        RuleFor(x => x.RegionName).NotEmpty().When(x => Has(x.RegionCode)).MaximumLength(150);
        RuleFor(x => x.ProvinceName).NotEmpty().When(x => Has(x.ProvinceCode)).MaximumLength(150);
        RuleFor(x => x.CityMunicipalityName).NotEmpty().When(x => Has(x.CityMunicipalityCode)).MaximumLength(150);
        RuleFor(x => x.BarangayName).NotEmpty().When(x => Has(x.BarangayCode)).MaximumLength(150);

        RuleFor(x => x.CityMunicipalityCode).NotEmpty().When(x => Has(x.BarangayCode))
            .WithMessage("Choose a city/municipality before a barangay.");
        RuleFor(x => x.RegionCode).NotEmpty().When(x => Has(x.CityMunicipalityCode))
            .WithMessage("Choose a region before a city/municipality.");
        RuleFor(x => x.ZipCode).MaximumLength(10);
    }

    private static bool Has(string? v) => !string.IsNullOrWhiteSpace(v);
    private static bool BeTenDigits(string? v) => v != null && v.Trim().Length == 10 && v.Trim().All(char.IsAsciiDigit);
}

public class LearnerProfileDtoValidator : AbstractValidator<LearnerProfileDto>
{
    public LearnerProfileDtoValidator()
    {
        RuleFor(x => x.ExtensionName).MaximumLength(10);
        RuleFor(x => x.PsaBirthCertificateNumber).MaximumLength(30);
        RuleFor(x => x.PlaceOfBirth).MaximumLength(200);
        RuleFor(x => x.MotherTongue).MaximumLength(100);
        RuleFor(x => x.Religion).MaximumLength(100);
        RuleFor(x => x.IpCommunity).NotEmpty().When(x => x.IsIndigenousPeople)
            .WithMessage("Name the Indigenous Peoples community.").MaximumLength(100);
        RuleFor(x => x.FourPsHouseholdId).NotEmpty().When(x => x.Is4PsBeneficiary)
            .WithMessage("Enter the 4Ps household ID.").MaximumLength(30);
        RuleFor(x => x.DisabilityTypes).NotEmpty().When(x => x.HasDisability)
            .WithMessage("Select at least one disability type.");
        RuleForEach(x => x.DisabilityTypes).Must(t => DepEdLists.DisabilityTypes.Contains(t))
            .When(x => x.HasDisability)
            .WithMessage("'{PropertyValue}' is not a recognized disability type.");
        RuleFor(x => x.FatherLastName).MaximumLength(100);
        RuleFor(x => x.FatherFirstName).MaximumLength(100);
        RuleFor(x => x.FatherMiddleName).MaximumLength(100);
        RuleFor(x => x.FatherContactNumber).MaximumLength(50);
        RuleFor(x => x.MotherMaidenLastName).MaximumLength(100);
        RuleFor(x => x.MotherMaidenFirstName).MaximumLength(100);
        RuleFor(x => x.MotherMaidenMiddleName).MaximumLength(100);
        RuleFor(x => x.MotherContactNumber).MaximumLength(50);
        RuleFor(x => x.HomeAddress!).SetValidator(new PsgcAddressDtoValidator()).When(x => x.HomeAddress != null);
    }
}

public class EnrollmentLearnerInfoDtoValidator : AbstractValidator<EnrollmentLearnerInfoDto>
{
    public EnrollmentLearnerInfoDtoValidator()
    {
        RuleFor(x => x.LearnerType).Must(t => DepEdLists.LearnerTypes.Contains(t!.Trim())).When(x => Has(x.LearnerType))
            .WithMessage("Learner type must be New, Continuing, Transferee, or Returning.");
        RuleFor(x => x.LastGradeLevelCompleted).Must(g => GradeLevels.All.Contains(g!.Trim())).When(x => Has(x.LastGradeLevelCompleted))
            .WithMessage("Choose the last grade level completed from the list.");
        RuleFor(x => x.LastSchoolYearCompleted).Matches("^[0-9]{4}-[0-9]{4}$").When(x => Has(x.LastSchoolYearCompleted))
            .WithMessage("Use the school-year format 2025-2026.");
        RuleFor(x => x.LastSchoolAttended).MaximumLength(200);
        RuleFor(x => x.LastSchoolAddress).MaximumLength(500);
        RuleFor(x => x.LastSchoolId).Matches("^[0-9]{6}$").When(x => Has(x.LastSchoolId))
            .WithMessage("A DepEd School ID is 6 digits.");
        RuleFor(x => x.Track).MaximumLength(100);
        RuleFor(x => x.Strand).MaximumLength(100);
    }

    private static bool Has(string? v) => !string.IsNullOrWhiteSpace(v);
}
