using Enrollify.Application.Common;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>Persistence shape of the learner record: owned address, nullable LRN, new columns.</summary>
public class LearnerRecordModelTests
{
    [Fact]
    public async Task Student_WithNoAddress_ReloadsWithEmptyNonNullHomeAddress()
    {
        var dbName = $"model-{Guid.NewGuid()}";
        using (var ctx = TestDb.Create(dbName))
        {
            ctx.Students.Add(new Student { StudentNumber = "2026-00001", FirstName = "Ana", LastName = "Cruz", Address = "" });
            await ctx.SaveChangesAsync();
        }

        using var verify = TestDb.Create(dbName);
        var s = await verify.Students.SingleAsync();
        Assert.NotNull(s.HomeAddress);
        Assert.Null(s.HomeAddress.BarangayCode);
        Assert.False(s.HomeAddress.IsMatched);
        Assert.Null(s.LRN);
        Assert.False(s.LrnNeedsReview);
    }

    [Fact]
    public async Task Student_LearnerProfileAndAddress_RoundTrip()
    {
        var dbName = $"model-{Guid.NewGuid()}";
        using (var ctx = TestDb.Create(dbName))
        {
            ctx.Students.Add(new Student
            {
                StudentNumber = "2026-00002", LRN = "136512260001", FirstName = "Ben", LastName = "Reyes", Address = "12 Mabini St",
                ExtensionName = "Jr.", PsaBirthCertificateNumber = "PSA-123", PlaceOfBirth = "Quezon City",
                MotherTongue = "Tagalog", Religion = "Catholic",
                IsIndigenousPeople = true, IpCommunity = "Aeta",
                Is4PsBeneficiary = true, FourPsHouseholdId = "HH-555",
                HasDisability = true, DisabilityTypes = "Hearing Impairment,Cancer",
                FatherLastName = "Reyes", FatherFirstName = "Carlos", MotherMaidenLastName = "Santos", MotherMaidenFirstName = "Liza",
                HomeAddress = new Domain.ValueObjects.PsgcAddress
                {
                    RegionCode = "1300000000", RegionName = "NCR",
                    CityMunicipalityCode = "1380600000", CityMunicipalityName = "City of Manila",
                    BarangayCode = "1380601001", BarangayName = "Barangay 1", ZipCode = "1013"
                }
            });
            await ctx.SaveChangesAsync();
        }

        using var verify = TestDb.Create(dbName);
        var s = await verify.Students.SingleAsync();
        Assert.Equal("136512260001", s.LRN);
        Assert.Equal("Aeta", s.IpCommunity);
        Assert.Equal("Hearing Impairment,Cancer", s.DisabilityTypes);
        Assert.Equal("1380601001", s.HomeAddress.BarangayCode);
        Assert.Null(s.HomeAddress.ProvinceCode);
        Assert.True(s.HomeAddress.IsMatched);
    }

    [Fact]
    public async Task Enrollment_LearnerInfoColumns_RoundTrip()
    {
        var dbName = $"model-{Guid.NewGuid()}";
        Guid enrollmentId;
        using (var ctx = TestDb.Create(dbName))
        {
            var student = new Student { StudentNumber = "2026-00003", FirstName = "Cy", LastName = "Uy", Address = "" };
            ctx.Students.Add(student);
            var e = new Enrollment
            {
                StudentId = student.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 11", Status = EnrollmentStatus.Draft,
                LearnerType = "Transferee", LastGradeLevelCompleted = "Grade 10", LastSchoolYearCompleted = "2025-2026",
                LastSchoolAttended = "Rizal HS", LastSchoolAddress = "Pasig", LastSchoolId = "305123",
                Track = "Academic", Strand = "STEM"
            };
            ctx.Enrollments.Add(e);
            await ctx.SaveChangesAsync();
            enrollmentId = e.Id;
        }

        using var verify = TestDb.Create(dbName);
        var reloaded = await verify.Enrollments.SingleAsync(x => x.Id == enrollmentId);
        Assert.Equal("Transferee", reloaded.LearnerType);
        Assert.Equal("305123", reloaded.LastSchoolId);
        Assert.Equal("STEM", reloaded.Strand);
    }

    [Fact]
    public async Task ShsStrand_IsTenantScoped()
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = true });
        ctx.ShsStrands.Add(new ShsStrand { TenantId = Guid.NewGuid(), Track = "Academic", Strand = "ABM", IsActive = true });
        await ctx.SaveChangesAsync();

        var visible = await ctx.ShsStrands.Select(s => s.Strand).ToListAsync();
        Assert.Equal(new[] { "STEM" }, visible);
    }

    [Fact]
    public async Task EnrollmentSearch_StudentWithoutLrn_DoesNotThrow()
    {
        var ctx = TestDb.Create();
        var student = new Student { StudentNumber = "2026-00004", FirstName = "Dee", LastName = "Lim", Address = "" };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(new Enrollment { StudentId = student.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 7", Status = EnrollmentStatus.Draft });
        await ctx.SaveChangesAsync();

        var result = await new GetEnrollmentsQueryHandler(ctx).Handle(
            new GetEnrollmentsQuery(null, null, null, "dee"), default);

        Assert.Single(result.Items);
    }

    [Theory]
    [InlineData("Grade 11", true)]
    [InlineData(" grade 12 ", true)]
    [InlineData("Grade 10", false)]
    [InlineData(null, false)]
    public void IsShsGrade(string? grade, bool expected) => Assert.Equal(expected, DepEdLists.IsShsGrade(grade));

    [Theory]
    [InlineData("136512260001", true)]
    [InlineData("13651226000", false)]
    [InlineData("13651226000A", false)]
    [InlineData("１３６５１２２６０００１", false)] // full-width digits are not ASCII digits
    [InlineData(null, false)]
    public void IsTwelveDigitLrn(string? value, bool expected) => Assert.Equal(expected, DepEdLists.IsTwelveDigitLrn(value));
}
