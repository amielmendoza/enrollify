using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// LIS worklist: section counts (incl. the no-section group), school-year default, the
/// Continuing vs Search-or-register split, fix reasons, tags, listed rules, and ordering.
/// </summary>
public class LisWorklistReportTests
{
    private const string Year = "2025-2026";

    private static ApplicationDbContext NewDb(bool withTenant = true)
    {
        var ctx = TestDb.Create();
        if (withTenant)
            ctx.Tenants.Add(new Tenant
            {
                Id = TestDb.TenantId, Name = "Metro Science HS", Subdomain = "mshs",
                DepEdSchoolId = "301234", DepEdRegionName = "NCR", DepEdDivision = "Manila", DepEdDistrict = "District 3"
            });
        ctx.SchoolYears.AddRange(
            new SchoolYear { Name = "2024-2025", StartDate = new DateTime(2024, 6, 1), EndDate = new DateTime(2025, 3, 31), IsActive = false },
            new SchoolYear { Name = Year, StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true });
        return ctx;
    }

    private static Section AddSection(ApplicationDbContext ctx, string grade, string name, string year = Year, string? adviser = null)
    {
        var s = new Section { Name = name, GradeLevel = grade, SchoolYear = year, Capacity = 40, Adviser = adviser };
        ctx.Sections.Add(s);
        return s;
    }

    /// <summary>A fully-complete listed learner by default (no fix reasons); override via parameters/configure.</summary>
    private static Enrollment AddLearner(
        ApplicationDbContext ctx, Section? section, string last, string first, string middle = "M",
        EnrollmentStatus status = EnrollmentStatus.Enrolled, string? learnerType = "Continuing",
        string? lrn = "123456789012", string? gender = "Male", bool lrnReview = false,
        Action<Student>? student = null, Action<Enrollment>? enrollment = null, string grade = "Grade 7", string year = Year)
    {
        var st = new Student
        {
            FirstName = first, LastName = last, MiddleName = middle, LRN = lrn, Gender = gender,
            BirthDate = new DateTime(2012, 5, 5), LrnNeedsReview = lrnReview, Address = "Manila"
        };
        student?.Invoke(st);
        ctx.Students.Add(st);

        var e = new Enrollment
        {
            StudentId = st.Id, SchoolYear = year, GradeLevel = section?.GradeLevel ?? grade, SectionId = section?.Id,
            Status = status, LearnerType = learnerType
        };
        enrollment?.Invoke(e);
        ctx.Enrollments.Add(e);
        return e;
    }

    private static Task<LisWorklistDto> Run(ApplicationDbContext ctx, string? year = Year, Guid? sectionId = null, bool noSection = false) =>
        new GetLisWorklistQueryHandler(ctx).Handle(
            new GetLisWorklistQuery(TestDb.TenantId, year, sectionId, noSection), CancellationToken.None);

    private static IEnumerable<WorklistLearnerDto> All(WorklistSectionDetailDto d) => d.Continuing.Concat(d.SearchOrRegister);

    // ----- Header -----

    [Fact]
    public async Task Header_comes_from_the_tenant_profile_and_resolved_year()
    {
        var ctx = NewDb();
        await ctx.SaveChangesAsync();

        var result = await Run(ctx);

        Assert.Equal(new SchoolHeaderDto("Metro Science HS", "301234", "NCR", "Manila", "District 3", Year), result.Header);
    }

    [Fact]
    public async Task Header_leaves_unset_profile_fields_null()
    {
        var ctx = NewDb(withTenant: false);
        ctx.Tenants.Add(new Tenant { Id = TestDb.TenantId, Name = "Bare School", Subdomain = "bare" });
        await ctx.SaveChangesAsync();

        var h = (await Run(ctx)).Header;

        Assert.Equal("Bare School", h.SchoolName);
        Assert.Null(h.SchoolId);
        Assert.Null(h.RegionName);
        Assert.Null(h.Division);
        Assert.Null(h.District);
    }

    // ----- Sections -----

    [Fact]
    public async Task Sections_are_ordered_by_grade_then_name_and_scoped_to_the_year()
    {
        var ctx = NewDb();
        AddSection(ctx, "Grade 10", "Alpha");
        AddSection(ctx, "Grade 7", "Zeta", adviser: "Ms. Cruz");
        AddSection(ctx, "Grade 7", "Alpha");
        AddSection(ctx, "Kindergarten", "Sunflower");
        AddSection(ctx, "Grade 7", "OldYear", year: "2024-2025");
        await ctx.SaveChangesAsync();

        var sections = (await Run(ctx)).Sections.Where(s => s.SectionId != null).ToList();

        Assert.Equal(
            new[] { "Kindergarten/Sunflower", "Grade 7/Alpha", "Grade 7/Zeta", "Grade 10/Alpha" },
            sections.Select(s => $"{s.GradeLevel}/{s.SectionName}"));
        Assert.Equal("Ms. Cruz", sections.Single(s => s.SectionName == "Zeta").Adviser);
    }

    [Fact]
    public async Task Section_counts_cover_listed_learners_with_encoded_and_pending()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "A", "A", enrollment: e => { e.LisEncodedAt = DateTime.UtcNow; e.LisEncodedBy = "reg"; });
        AddLearner(ctx, s, "B", "B", status: EnrollmentStatus.Approved);
        AddLearner(ctx, s, "C", "C", status: EnrollmentStatus.Paid);
        AddLearner(ctx, s, "D", "D", status: EnrollmentStatus.Submitted);   // not listed
        AddLearner(ctx, s, "E", "E", status: EnrollmentStatus.Cancelled);   // not listed
        AddLearner(ctx, s, "F", "F", status: EnrollmentStatus.Draft);       // not listed
        await ctx.SaveChangesAsync();

        var row = (await Run(ctx)).Sections.Single(x => x.SectionId == s.Id);

        Assert.Equal(3, row.Listed);
        Assert.Equal(1, row.Encoded);
        Assert.Equal(2, row.Pending);
    }

    [Fact]
    public async Task No_section_group_counts_listed_learners_without_a_section()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "In", "Sec");
        AddLearner(ctx, null, "No", "Sec1");
        AddLearner(ctx, null, "No", "Sec2", enrollment: e => e.LisEncodedAt = DateTime.UtcNow);
        AddLearner(ctx, null, "No", "Sec3", status: EnrollmentStatus.Cancelled);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx);
        var none = result.Sections.Single(x => x.SectionId == null);

        Assert.Equal(2, none.Listed);
        Assert.Equal(1, none.Encoded);
        Assert.Equal(1, none.Pending);
        Assert.Equal(1, result.Sections.Single(x => x.SectionId == s.Id).Listed);
    }

    [Fact]
    public async Task No_section_group_is_present_even_when_empty()
    {
        var ctx = NewDb();
        AddSection(ctx, "Grade 7", "Einstein");
        await ctx.SaveChangesAsync();

        var none = (await Run(ctx)).Sections.Single(x => x.SectionId == null);

        Assert.Equal(0, none.Listed);
    }

    [Fact]
    public async Task Other_years_enrollments_are_not_counted()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "Cur", "Rent");
        AddLearner(ctx, s, "Old", "Year", year: "2024-2025");
        await ctx.SaveChangesAsync();

        Assert.Equal(1, (await Run(ctx)).Sections.Single(x => x.SectionId == s.Id).Listed);
    }

    [Fact]
    public async Task School_year_defaults_to_the_active_year()
    {
        var ctx = NewDb();
        var cur = AddSection(ctx, "Grade 7", "Current");
        var old = AddSection(ctx, "Grade 7", "Old", year: "2024-2025");
        AddLearner(ctx, cur, "Cur", "Rent");
        AddLearner(ctx, old, "Old", "Year", year: "2024-2025");
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, year: null);

        Assert.Equal(Year, result.Header.SchoolYear);
        Assert.Equal(new[] { cur.Id }, result.Sections.Where(x => x.SectionId != null).Select(x => x.SectionId!.Value));

        var explicitOld = await Run(ctx, year: "2024-2025");
        Assert.Equal("2024-2025", explicitOld.Header.SchoolYear);
        Assert.Equal(new[] { old.Id }, explicitOld.Sections.Where(x => x.SectionId != null).Select(x => x.SectionId!.Value));
    }

    [Fact]
    public async Task Selected_is_null_unless_a_group_is_requested()
    {
        var ctx = NewDb();
        AddSection(ctx, "Grade 7", "Einstein");
        await ctx.SaveChangesAsync();

        Assert.Null((await Run(ctx)).Selected);
    }

    // ----- Selected: split and ordering -----

    [Fact]
    public async Task Selected_splits_continuing_from_search_or_register()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein", adviser: "Mr. Reyes");
        AddLearner(ctx, s, "Cont", "Inuing", learnerType: "Continuing");
        AddLearner(ctx, s, "New", "Kid", learnerType: "New", lrn: null);
        AddLearner(ctx, s, "Trans", "Feree", learnerType: "Transferee");
        AddLearner(ctx, s, "Ret", "Urning", learnerType: "Returning");
        AddLearner(ctx, s, "Un", "Typed", learnerType: null);
        AddLearner(ctx, null, "Else", "Where");
        await ctx.SaveChangesAsync();

        var sel = (await Run(ctx, sectionId: s.Id)).Selected!;

        Assert.Equal(s.Id, sel.SectionId);
        Assert.Equal("Grade 7", sel.GradeLevel);
        Assert.Equal("Einstein", sel.SectionName);
        Assert.Equal("Mr. Reyes", sel.Adviser);
        Assert.Equal(new[] { "Continuing" }, sel.Continuing.Select(l => l.LearnerType));
        Assert.Equal(4, sel.SearchOrRegister.Count);
        Assert.DoesNotContain(sel.SearchOrRegister, l => l.LearnerType == "Continuing");
    }

    [Fact]
    public async Task Blank_learner_type_lands_in_search_or_register()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "Cruz", "Ana", learnerType: "  ");
        await ctx.SaveChangesAsync();

        var sel = (await Run(ctx, sectionId: s.Id)).Selected!;

        Assert.Empty(sel.Continuing);
        Assert.Single(sel.SearchOrRegister);
    }

    [Fact]
    public async Task Both_groups_are_sorted_by_last_first_middle()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "Santos", "Ben", "B");
        AddLearner(ctx, s, "Cruz", "Zed", "A");
        AddLearner(ctx, s, "Cruz", "Ana", "Z");
        AddLearner(ctx, s, "Cruz", "Ana", "C");
        AddLearner(ctx, s, "Reyes", "Zoe", learnerType: "Transferee");
        AddLearner(ctx, s, "Abad", "Yan", learnerType: "New", lrn: null);
        AddLearner(ctx, s, "Abad", "Cai", learnerType: "Returning");
        await ctx.SaveChangesAsync();

        var sel = (await Run(ctx, sectionId: s.Id)).Selected!;

        Assert.Equal(
            new[] { "Cruz, Ana, C", "Cruz, Ana, Z", "Cruz, Zed, A", "Santos, Ben, B" },
            sel.Continuing.Select(l => l.Name));
        Assert.Equal(
            new[] { "Abad, Cai, M", "Abad, Yan, M", "Reyes, Zoe, M" },
            sel.SearchOrRegister.Select(l => l.Name));
    }

    [Fact]
    public async Task No_section_group_can_be_selected()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "In", "Section");
        AddLearner(ctx, null, "Floating", "One");
        AddLearner(ctx, null, "Floating", "Two", learnerType: "Transferee");
        await ctx.SaveChangesAsync();

        var sel = (await Run(ctx, noSection: true)).Selected!;

        Assert.Null(sel.SectionId);
        Assert.Equal(new[] { "Floating, One, M" }, sel.Continuing.Select(l => l.Name));
        Assert.Equal(new[] { "Floating, Two, M" }, sel.SearchOrRegister.Select(l => l.Name));
    }

    [Fact]
    public async Task Selected_lists_only_listed_learners_but_keeps_withdrawn_ones_with_their_mark()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "Active", "One");
        AddLearner(ctx, s, "Dropped", "Two", enrollment: e =>
        {
            e.WithdrawalType = "Dropped";
            e.WithdrawalDate = new DateTime(2025, 9, 1);
        });
        AddLearner(ctx, s, "Cancelled", "Three", status: EnrollmentStatus.Cancelled);
        AddLearner(ctx, s, "Submitted", "Four", status: EnrollmentStatus.Submitted);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, sectionId: s.Id);
        var learners = All(result.Selected!).ToList();

        Assert.Equal(2, learners.Count);
        Assert.Equal(2, result.Sections.Single(x => x.SectionId == s.Id).Listed);
        var dropped = learners.Single(l => l.Name.StartsWith("Dropped"));
        Assert.Equal("Dropped", dropped.WithdrawalType);
        Assert.Equal(new DateTime(2025, 9, 1), dropped.WithdrawalDate);
    }

    // ----- Learner fields -----

    [Fact]
    public async Task Learner_fields_are_projected()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        var encodedAt = new DateTime(2025, 6, 20, 3, 0, 0, DateTimeKind.Utc);
        var e = AddLearner(ctx, s, "Cruz", "Ana", "Dela", gender: "Female",
            student: st =>
            {
                st.ExtensionName = "Jr.";
                st.BirthDate = new DateTime(2012, 3, 4);
            },
            enrollment: en =>
            {
                en.FirstAttendanceDate = new DateTime(2025, 6, 10);
                en.LisEncodedAt = encodedAt;
                en.LisEncodedBy = "registrar";
            });
        await ctx.SaveChangesAsync();

        var l = All((await Run(ctx, sectionId: s.Id)).Selected!).Single();

        Assert.Equal(e.Id, l.EnrollmentId);
        Assert.Equal(e.StudentId, l.StudentId);
        Assert.Equal("123456789012", l.Lrn);
        Assert.False(l.LrnNeedsReview);
        Assert.Equal("Cruz, Ana, Jr., Dela", l.Name);
        Assert.Equal("Female", l.Sex);
        Assert.Equal(new DateTime(2012, 3, 4), l.BirthDate);
        Assert.Equal("Continuing", l.LearnerType);
        Assert.Equal(new DateTime(2025, 6, 10), l.FirstAttendance);
        Assert.Equal(encodedAt, l.LisEncodedAt);
        Assert.Equal("registrar", l.LisEncodedBy);
        Assert.Empty(l.FixReasons);
        Assert.Empty(l.Tags);
    }

    [Fact]
    public async Task First_attendance_defaults_to_the_school_year_start()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "Cruz", "Ana");
        await ctx.SaveChangesAsync();

        var l = All((await Run(ctx, sectionId: s.Id)).Selected!).Single();

        Assert.Equal(new DateTime(2025, 6, 1), l.FirstAttendance);
    }

    // ----- Tags -----

    [Fact]
    public async Task Tags_flag_4ps_balik_aral_and_repeater()
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        AddLearner(ctx, s, "Aa", "One", learnerType: "Returning", student: st => st.Is4PsBeneficiary = true,
            enrollment: en => en.IsRepeater = true);
        AddLearner(ctx, s, "Bb", "Two", learnerType: "Transferee");
        await ctx.SaveChangesAsync();

        var learners = All((await Run(ctx, sectionId: s.Id)).Selected!).ToDictionary(l => l.Name);

        Assert.Equal(new[] { "4Ps", "Balik-aral", "Repeater" }, learners["Aa, One, M"].Tags);
        Assert.Empty(learners["Bb, Two, M"].Tags);
    }

    // ----- Fix reasons -----

    private static async Task<WorklistLearnerDto> OneLearner(Action<ApplicationDbContext, Section> seed)
    {
        var ctx = NewDb();
        var s = AddSection(ctx, "Grade 7", "Einstein");
        seed(ctx, s);
        await ctx.SaveChangesAsync();
        return All((await Run(ctx, sectionId: s.Id)).Selected!).Single();
    }

    [Fact]
    public async Task Fix_reason_sex_not_set()
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", gender: null));

        Assert.Equal(new[] { "Sex not set" }, l.FixReasons);
        Assert.Equal("NotSet", l.Sex);
    }

    [Fact]
    public async Task Fix_reason_no_birth_date()
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", student: st => st.BirthDate = default));

        Assert.Equal(new[] { "No birth date" }, l.FixReasons);
        Assert.Null(l.BirthDate);
    }

    [Fact]
    public async Task Fix_reason_lrn_needs_checking()
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", lrnReview: true));

        Assert.Equal(new[] { "LRN needs checking" }, l.FixReasons);
        Assert.True(l.LrnNeedsReview);
    }

    [Theory]
    [InlineData("Continuing")]
    [InlineData("Transferee")]
    [InlineData("Returning")]
    public async Task Fix_reason_no_lrn_when_not_a_new_learner(string learnerType)
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", learnerType: learnerType, lrn: null));

        Assert.Equal(new[] { "No LRN" }, l.FixReasons);
    }

    [Fact]
    public async Task Whitespace_lrn_counts_as_no_lrn()
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", learnerType: "Continuing", lrn: "  "));

        Assert.Equal(new[] { "No LRN" }, l.FixReasons);
    }

    [Fact]
    public async Task A_new_learner_without_an_lrn_raises_no_reason()
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", learnerType: "New", lrn: null));

        Assert.Empty(l.FixReasons);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fix_reason_learner_type_not_set(string? learnerType)
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", learnerType: learnerType));

        Assert.Equal(new[] { "Learner type not set" }, l.FixReasons);
    }

    [Fact]
    public async Task A_learner_can_have_several_fix_reasons_in_a_stable_order()
    {
        var l = await OneLearner((c, s) => AddLearner(c, s, "Cruz", "Ana", gender: "", lrn: null, learnerType: null, lrnReview: true,
            student: st => st.BirthDate = default));

        Assert.Equal(
            new[] { "Sex not set", "No birth date", "LRN needs checking", "No LRN", "Learner type not set" },
            l.FixReasons);
    }
}
