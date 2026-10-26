using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Domain.ValueObjects;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// SF1 working copy: a section's listed learners grouped Male / Female / NotSet and sorted A-Z,
/// the SF1 columns, totals, and the CSV that mirrors the screen.
/// </summary>
public class Sf1ReportTests
{
    private const string Year = "2025-2026";

    private static ApplicationDbContext NewDb(out Section section)
    {
        var ctx = TestDb.Create();
        ctx.Tenants.Add(new Tenant
        {
            Id = TestDb.TenantId, Name = "Metro Science HS", Subdomain = "mshs",
            DepEdSchoolId = "301234", DepEdRegionName = "NCR", DepEdDivision = "Manila", DepEdDistrict = "District 3",
            SchoolHeadName = "Dr. Elena Ramos"
        });
        ctx.SchoolYears.Add(new SchoolYear
        {
            Name = Year, StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true
        });
        section = new Section { Name = "Rizal", GradeLevel = "Grade 7", SchoolYear = Year, Capacity = 40, Adviser = "Ms. Dela Cruz" };
        ctx.Sections.Add(section);
        return ctx;
    }

    private static Enrollment Add(
        ApplicationDbContext ctx, Section section, string? gender, string last, string first, string middle = "",
        EnrollmentStatus status = EnrollmentStatus.Enrolled, Action<Student>? tweak = null, string year = Year)
    {
        var st = new Student
        {
            FirstName = first, LastName = last, MiddleName = middle, Gender = gender,
            Address = "12 Mabini St", BirthDate = new DateTime(2012, 8, 15)
        };
        tweak?.Invoke(st);
        ctx.Students.Add(st);
        var e = new Enrollment
        {
            StudentId = st.Id, SchoolYear = year, GradeLevel = section.GradeLevel, SectionId = section.Id, Status = status
        };
        ctx.Enrollments.Add(e);
        return e;
    }

    private static Task<Sf1Dto> Run(ApplicationDbContext ctx, Section section, string? year = Year) =>
        new GetSf1QueryHandler(ctx).Handle(new GetSf1Query(TestDb.TenantId, year, section.Id), CancellationToken.None);

    [Fact]
    public async Task Header_comes_from_tenant_section_and_year()
    {
        var ctx = NewDb(out var section);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, section, year: null);

        Assert.Equal(
            new Sf1HeaderDto(
                new SchoolHeaderDto("Metro Science HS", "301234", "NCR", "Manila", "District 3", Year),
                "Grade 7", "Rizal", "Ms. Dela Cruz", "Dr. Elena Ramos"),
            result.Header);
        Assert.Empty(result.Learners);
        Assert.Equal((0, 0, 0, 0), (result.Male, result.Female, result.NotSet, result.Total));
    }

    [Fact]
    public async Task Learners_are_grouped_male_female_notset_each_sorted_az()
    {
        var ctx = NewDb(out var section);
        Add(ctx, section, "Female", "Bautista", "Ana");
        Add(ctx, section, "Male", "Santos", "Pedro");
        Add(ctx, section, null, "Zamora", "Jo");
        Add(ctx, section, "F", "Aquino", "Mia");
        Add(ctx, section, "male", "Reyes", "Juan", "B");
        Add(ctx, section, "Male", "Reyes", "Juan", "A");
        Add(ctx, section, "Male", "Reyes", "Alan");
        Add(ctx, section, "Other", "Dizon", "Kai");
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, section);

        Assert.Equal(
            new[]
            {
                "Reyes, Alan", "Reyes, Juan, A", "Reyes, Juan, B", "Santos, Pedro",   // male
                "Aquino, Mia", "Bautista, Ana",                                       // female
                "Dizon, Kai", "Zamora, Jo"                                            // not set
            },
            result.Learners.Select(l => l.Name).ToArray());
        Assert.Equal(
            new[] { "M", "M", "M", "M", "F", "F", "", "" },
            result.Learners.Select(l => l.Sex).ToArray());
        Assert.Equal((4, 2, 2, 8), (result.Male, result.Female, result.NotSet, result.Total));
    }

    [Fact]
    public async Task Only_listed_enrollments_of_this_section_and_year_are_included()
    {
        var ctx = NewDb(out var section);
        var other = new Section { Name = "Bonifacio", GradeLevel = "Grade 7", SchoolYear = Year, Capacity = 40 };
        ctx.Sections.Add(other);
        Add(ctx, section, "Male", "Approved", "A", status: EnrollmentStatus.Approved);
        Add(ctx, section, "Male", "Paid", "A", status: EnrollmentStatus.Paid);
        Add(ctx, section, "Male", "Enrolled", "A", status: EnrollmentStatus.Enrolled);
        Add(ctx, section, "Male", "Draft", "A", status: EnrollmentStatus.Draft);
        Add(ctx, section, "Male", "Submitted", "A", status: EnrollmentStatus.Submitted);
        Add(ctx, section, "Male", "Cancelled", "A", status: EnrollmentStatus.Cancelled);
        Add(ctx, other, "Male", "OtherSection", "A");
        Add(ctx, section, "Male", "OldYear", "A", year: "2024-2025");
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, section);

        Assert.Equal(new[] { "Approved, A", "Enrolled, A", "Paid, A" }, result.Learners.Select(l => l.Name).ToArray());
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task Row_columns_come_from_the_student_and_home_address_names()
    {
        var ctx = NewDb(out var section);
        Add(ctx, section, "Female", "Cruz", "Maria", "Lopez", tweak: s =>
        {
            s.LRN = "123456789012";
            s.ExtensionName = "Jr.";
            s.BirthDate = new DateTime(2012, 8, 15);
            s.MotherTongue = "Tagalog";
            s.Religion = "Catholic";
            s.Address = "45 Sampaguita St";
            s.HomeAddress = new PsgcAddress
            {
                BarangayCode = "1380600001", BarangayName = "Barangay 1",
                CityMunicipalityCode = "1380600000", CityMunicipalityName = "Manila",
                ProvinceCode = "1380000000", ProvinceName = "Metro Manila"
            };
            s.FatherLastName = "Cruz"; s.FatherFirstName = "Jose"; s.FatherMiddleName = "Reyes";
            s.MotherMaidenLastName = "Lopez"; s.MotherMaidenFirstName = "Carmen"; s.MotherMaidenMiddleName = "Diaz";
            s.GuardianName = "Tita Rosa"; s.GuardianRelationship = "Aunt";
            s.GuardianContact = "0917-111"; s.MotherContactNumber = "0917-222"; s.FatherContactNumber = "0917-333";
            s.Is4PsBeneficiary = true;
        });
        await ctx.SaveChangesAsync();

        var row = Assert.Single((await Run(ctx, section)).Learners);

        Assert.Equal("123456789012", row.Lrn);
        Assert.Equal("Cruz, Maria, Jr., Lopez", row.Name);
        Assert.Equal("F", row.Sex);
        Assert.Equal(new DateTime(2012, 8, 15), row.BirthDate);
        Assert.Equal("Tagalog", row.MotherTongue);
        Assert.Equal("Catholic", row.Religion);
        Assert.Equal("45 Sampaguita St", row.HouseStreet);
        Assert.Equal("Barangay 1", row.Barangay);
        Assert.Equal("Manila", row.CityMunicipality);
        Assert.Equal("Metro Manila", row.Province);
        Assert.Equal("Cruz, Jose, Reyes", row.FatherName);
        Assert.Equal("Lopez, Carmen, Diaz", row.MotherMaidenName);
        Assert.Equal("Tita Rosa", row.GuardianName);
        Assert.Equal("Aunt", row.GuardianRelationship);
        Assert.Equal("0917-111", row.Contact);
        Assert.Equal("CCT", row.Remarks);
    }

    [Fact]
    public async Task Ip_group_is_filled_only_when_the_learner_is_indigenous()
    {
        var ctx = NewDb(out var section);
        Add(ctx, section, "Male", "Aeta", "Ben", tweak: s => { s.IsIndigenousPeople = true; s.IpCommunity = "Aeta"; });
        Add(ctx, section, "Male", "Stale", "Sam", tweak: s => { s.IsIndigenousPeople = false; s.IpCommunity = "Aeta"; });
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, section);

        Assert.Equal("Aeta", result.Learners.Single(l => l.Name == "Aeta, Ben").IpGroup);
        Assert.True(string.IsNullOrEmpty(result.Learners.Single(l => l.Name == "Stale, Sam").IpGroup));
    }

    [Fact]
    public async Task Age_uses_first_friday_of_june_of_the_start_year()
    {
        var ctx = NewDb(out var section);
        // First Friday of June 2025 is 06/06/2025.
        Add(ctx, section, "Male", "Birthday", "On", tweak: s => s.BirthDate = new DateTime(2013, 6, 6));
        Add(ctx, section, "Male", "Birthday", "After", tweak: s => s.BirthDate = new DateTime(2013, 6, 7));
        Add(ctx, section, "Male", "Nodate", "Not", tweak: s => s.BirthDate = default);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, section);

        Assert.Equal(12, result.Learners.Single(l => l.Name == "Birthday, On").Age);
        Assert.Equal(11, result.Learners.Single(l => l.Name == "Birthday, After").Age);
        var none = result.Learners.Single(l => l.Name == "Nodate, Not");
        Assert.Null(none.Age);
        Assert.Null(none.BirthDate);
    }

    [Fact]
    public async Task Missing_section_throws_not_found()
    {
        var ctx = NewDb(out _);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GetSf1QueryHandler(ctx).Handle(new GetSf1Query(TestDb.TenantId, Year, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Export_query_returns_the_same_report()
    {
        var ctx = NewDb(out var section);
        Add(ctx, section, "Male", "Santos", "Pedro");
        Add(ctx, section, null, "Zamora", "Jo");
        await ctx.SaveChangesAsync();

        var screen = await Run(ctx, section);
        var export = await new GetSf1ExportQueryHandler(ctx)
            .Handle(new GetSf1ExportQuery(TestDb.TenantId, Year, section.Id), CancellationToken.None);

        Assert.Equal(screen.Header, export.Header);
        Assert.Equal(screen.Learners, export.Learners);
        Assert.Equal((screen.Male, screen.Female, screen.NotSet, screen.Total), (export.Male, export.Female, export.NotSet, export.Total));
    }

    [Fact]
    public async Task Csv_columns_match_the_screen_row_order()
    {
        var ctx = NewDb(out var section);
        Add(ctx, section, "Female", "Cruz", "Maria", "Lopez", tweak: s =>
        {
            s.LRN = "123456789012";
            s.BirthDate = new DateTime(2012, 8, 15);
            s.MotherTongue = "Tagalog"; s.Religion = "Catholic";
            s.IsIndigenousPeople = true; s.IpCommunity = "Aeta";
            s.Address = "45 Sampaguita St, Purok 2";
            s.HomeAddress = new PsgcAddress { BarangayName = "Barangay 1", CityMunicipalityName = "Manila", ProvinceName = "Metro Manila" };
            s.FatherLastName = "Cruz"; s.FatherFirstName = "Jose"; s.FatherMiddleName = "Reyes";
            s.MotherMaidenLastName = "Lopez"; s.MotherMaidenFirstName = "Carmen"; s.MotherMaidenMiddleName = "Diaz";
            s.GuardianName = "Tita Rosa"; s.GuardianRelationship = "Aunt"; s.GuardianContact = "0917-111";
        });
        Add(ctx, section, "Male", "=Evil", "Ed", tweak: s => { s.BirthDate = default; s.Address = string.Empty; });
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, section);
        var csv = Sf1CsvBuilder.Build(result);
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(
            "LRN,Name,Sex,Birth Date,Age,Mother Tongue,IP Group,Religion,House No./Street,Barangay,City/Municipality,Province," +
            "Father's Name,Mother's Maiden Name,Guardian,Relationship,Contact Number,Remarks",
            lines[0]);
        Assert.Equal(Sf1CsvBuilder.Header, lines[0]);
        Assert.Equal(3, lines.Length);

        // Male first (injection-guarded name, no birth date / age), then the female.
        Assert.Equal(",\"'=Evil, Ed\",M" + new string(',', 15), lines[1]);
        Assert.Equal(
            "123456789012,\"Cruz, Maria, Lopez\",F,08/15/2012,12,Tagalog,Aeta,Catholic,\"45 Sampaguita St, Purok 2\"," +
            "Barangay 1,Manila,Metro Manila,\"Cruz, Jose, Reyes\",\"Lopez, Carmen, Diaz\",Tita Rosa,Aunt,0917-111,",
            lines[2]);
    }
}
