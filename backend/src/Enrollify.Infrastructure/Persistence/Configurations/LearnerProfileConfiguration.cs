using Enrollify.Domain.Common;
using Enrollify.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

/// <summary>
/// Column mapping for the ILearnerProfile fields, shared by Student and AdmissionApplication.
/// Lengths here are mirrored by LearnerProfileDtoValidator / PsgcAddressDtoValidator.
/// </summary>
internal static class LearnerProfileConfiguration
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : class, ILearnerProfile
    {
        builder.Property<string?>(nameof(ILearnerProfile.ExtensionName)).HasMaxLength(10);
        builder.Property<string?>(nameof(ILearnerProfile.PsaBirthCertificateNumber)).HasMaxLength(30);
        builder.Property<string?>(nameof(ILearnerProfile.PlaceOfBirth)).HasMaxLength(200);
        builder.Property<string?>(nameof(ILearnerProfile.MotherTongue)).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.Religion)).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.IpCommunity)).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.FourPsHouseholdId)).HasMaxLength(30);
        builder.Property<string?>(nameof(ILearnerProfile.DisabilityTypes)).HasMaxLength(500);
        foreach (var name in new[]
                 {
                     nameof(ILearnerProfile.FatherLastName), nameof(ILearnerProfile.FatherFirstName), nameof(ILearnerProfile.FatherMiddleName),
                     nameof(ILearnerProfile.MotherMaidenLastName), nameof(ILearnerProfile.MotherMaidenFirstName), nameof(ILearnerProfile.MotherMaidenMiddleName),
                 })
            builder.Property<string?>(name).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.FatherContactNumber)).HasMaxLength(50);
        builder.Property<string?>(nameof(ILearnerProfile.MotherContactNumber)).HasMaxLength(50);

        builder.OwnsOne<PsgcAddress>(nameof(ILearnerProfile.HomeAddress), a =>
        {
            a.Property(p => p.RegionCode).HasColumnName("AddressRegionCode").HasMaxLength(10);
            a.Property(p => p.RegionName).HasColumnName("AddressRegionName").HasMaxLength(150);
            a.Property(p => p.ProvinceCode).HasColumnName("AddressProvinceCode").HasMaxLength(10);
            a.Property(p => p.ProvinceName).HasColumnName("AddressProvinceName").HasMaxLength(150);
            a.Property(p => p.CityMunicipalityCode).HasColumnName("AddressCityMunicipalityCode").HasMaxLength(10);
            a.Property(p => p.CityMunicipalityName).HasColumnName("AddressCityMunicipalityName").HasMaxLength(150);
            a.Property(p => p.BarangayCode).HasColumnName("AddressBarangayCode").HasMaxLength(10);
            a.Property(p => p.BarangayName).HasColumnName("AddressBarangayName").HasMaxLength(150);
            a.Property(p => p.ZipCode).HasColumnName("AddressZipCode").HasMaxLength(10);
            a.Ignore(p => p.IsMatched);
        });
        // Required owned navigation: always materialized, even when every column is NULL.
        builder.Navigation(nameof(ILearnerProfile.HomeAddress)).IsRequired();
    }
}
