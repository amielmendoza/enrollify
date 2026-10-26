using System.Globalization;
using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Xunit;

namespace Enrollify.Application.Tests;

public class DepEdRulesTests
{
    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) =>
        new(y, m, d, h, min, 0, DateTimeKind.Utc);

    private static DateTime Day(int y, int m, int d) => new(y, m, d);

    private static Enrollment Enr(EnrollmentStatus status, DateTime? createdAtUtc = null) => new()
    {
        Status = status,
        SchoolYear = "2026-2027",
        GradeLevel = "Grade 7",
        CreatedAt = createdAtUtc ?? Utc(2026, 1, 1),
    };

    private static EnrollmentStatusHistory Hist(EnrollmentStatus from, EnrollmentStatus to, DateTime utc) => new()
    {
        FromStatus = from, ToStatus = to, TransitionDate = utc,
    };

    private static SchoolYear Row(int y, int m, int d) => new()
    {
        Name = $"{y}-{y + 1}", StartDate = new DateTime(y, m, d), EndDate = new DateTime(y + 1, 3, 31),
    };

    // ----- Manila day -----

    [Fact]
    public void ManilaDate_rolls_over_at_1600_utc()
    {
        Assert.Equal(Day(2026, 6, 8), DepEdRules.ManilaDate(Utc(2026, 6, 8, 15, 59)));
        Assert.Equal(Day(2026, 6, 9), DepEdRules.ManilaDate(Utc(2026, 6, 8, 16, 0)));
    }

    [Fact]
    public void ManilaEndOfDayUtc_is_1600_utc_of_that_date()
    {
        Assert.Equal(Utc(2026, 6, 8, 16, 0), DepEdRules.ManilaEndOfDayUtc(Day(2026, 6, 8)));
    }

    // ----- Status on a day -----

    [Fact]
    public void Approved_2330_Manila_counts_that_day()
    {
        // 23:30 Manila on 8 Jun = 15:30 UTC on 8 Jun.
        var e = Enr(EnrollmentStatus.Approved);
        var h = new[] { Hist(EnrollmentStatus.Assessed, EnrollmentStatus.Approved, Utc(2026, 6, 8, 15, 30)) };
        Assert.True(DepEdRules.CountedOn(e, h, Day(2026, 6, 8)));
        Assert.Equal(EnrollmentStatus.Approved, DepEdRules.StatusOn(e, h, Day(2026, 6, 8)));
    }

    [Fact]
    public void Approved_0030_next_Manila_day_does_not_count()
    {
        // 00:30 Manila on 9 Jun = 16:30 UTC on 8 Jun.
        var e = Enr(EnrollmentStatus.Approved);
        var h = new[] { Hist(EnrollmentStatus.Assessed, EnrollmentStatus.Approved, Utc(2026, 6, 8, 16, 30)) };
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 6, 8)));
        Assert.True(DepEdRules.CountedOn(e, h, Day(2026, 6, 9)));
    }

    [Fact]
    public void Transition_exactly_at_end_of_day_belongs_to_next_day()
    {
        var e = Enr(EnrollmentStatus.Approved);
        var h = new[] { Hist(EnrollmentStatus.Assessed, EnrollmentStatus.Approved, Utc(2026, 6, 8, 16, 0)) };
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 6, 8)));
    }

    [Fact]
    public void Status_on_day_uses_last_row_regardless_of_input_order()
    {
        var e = Enr(EnrollmentStatus.Paid);
        var h = new[]
        {
            Hist(EnrollmentStatus.Approved, EnrollmentStatus.Paid, Utc(2026, 6, 5)),
            Hist(EnrollmentStatus.Draft, EnrollmentStatus.Submitted, Utc(2026, 6, 1)),
            Hist(EnrollmentStatus.Submitted, EnrollmentStatus.Approved, Utc(2026, 6, 3)),
        };
        Assert.Equal(EnrollmentStatus.Submitted, DepEdRules.StatusOn(e, h, Day(2026, 6, 2)));
        Assert.Equal(EnrollmentStatus.Approved, DepEdRules.StatusOn(e, h, Day(2026, 6, 4)));
        Assert.Equal(EnrollmentStatus.Paid, DepEdRules.StatusOn(e, h, Day(2026, 6, 20)));
    }

    [Fact]
    public void History_but_none_that_early_means_no_status()
    {
        var e = Enr(EnrollmentStatus.Approved);
        var h = new[] { Hist(EnrollmentStatus.Draft, EnrollmentStatus.Submitted, Utc(2026, 6, 10)) };
        Assert.Null(DepEdRules.StatusOn(e, h, Day(2026, 6, 1)));
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 6, 1)));
    }

    // ----- No history -----

    [Fact]
    public void No_history_uses_current_status_from_CreatedAt()
    {
        var e = Enr(EnrollmentStatus.Enrolled, createdAtUtc: Utc(2026, 6, 3, 2, 0)); // 10:00 Manila 3 Jun
        var none = Array.Empty<EnrollmentStatusHistory>();

        Assert.Equal(EnrollmentStatus.Enrolled, DepEdRules.StatusOn(e, none, Day(2026, 6, 3)));
        Assert.True(DepEdRules.CountedOn(e, none, Day(2026, 6, 30)));
        Assert.Null(DepEdRules.StatusOn(e, none, Day(2026, 6, 2)));
        Assert.False(DepEdRules.CountedOn(e, none, Day(2026, 6, 2)));
    }

    // ----- Cancelled after approval -----

    [Fact]
    public void Approved_then_cancelled_counts_before_not_after()
    {
        var e = Enr(EnrollmentStatus.Cancelled);
        var h = new[]
        {
            Hist(EnrollmentStatus.Assessed, EnrollmentStatus.Approved, Utc(2026, 6, 1)),
            Hist(EnrollmentStatus.Approved, EnrollmentStatus.Cancelled, Utc(2026, 6, 10, 2, 0)), // 10:00 Manila 10 Jun
        };
        Assert.True(DepEdRules.CountedOn(e, h, Day(2026, 6, 9)));
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 6, 10)));
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 6, 11)));
    }

    // ----- Withdrawn -----

    [Fact]
    public void Withdrawn_on_X_counts_on_X_minus_1_not_X()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.WithdrawalType = "Dropped";
        e.WithdrawalDate = Day(2026, 8, 15);
        var h = new[] { Hist(EnrollmentStatus.Paid, EnrollmentStatus.Enrolled, Utc(2026, 6, 1)) };

        Assert.True(DepEdRules.CountedOn(e, h, Day(2026, 8, 14)));
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 8, 15)));
        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 8, 16)));
    }

    [Fact]
    public void Withdrawal_date_with_a_time_part_still_counts_by_day()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.WithdrawalDate = new DateTime(2026, 8, 15, 13, 0, 0);
        var h = new[] { Hist(EnrollmentStatus.Paid, EnrollmentStatus.Enrolled, Utc(2026, 6, 1)) };

        Assert.False(DepEdRules.CountedOn(e, h, Day(2026, 8, 15)));
        Assert.True(DepEdRules.CountedOn(e, h, Day(2026, 8, 14)));
    }

    [Theory]
    [InlineData(EnrollmentStatus.Draft, false)]
    [InlineData(EnrollmentStatus.Submitted, false)]
    [InlineData(EnrollmentStatus.Assessed, false)]
    [InlineData(EnrollmentStatus.Approved, true)]
    [InlineData(EnrollmentStatus.Paid, true)]
    [InlineData(EnrollmentStatus.Enrolled, true)]
    [InlineData(EnrollmentStatus.Cancelled, false)]
    public void Listed_is_Approved_Paid_Enrolled_only(EnrollmentStatus status, bool expected)
    {
        Assert.Equal(expected, DepEdRules.Listed(Enr(status)));
    }

    [Fact]
    public void Withdrawn_learner_stays_listed()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.WithdrawalType = "TransferredOut";
        e.WithdrawalDate = Day(2026, 8, 15);
        Assert.True(DepEdRules.Listed(e));
    }

    // ----- Sex -----

    [Theory]
    [InlineData("male", "Male")]
    [InlineData(" M ", "Male")]
    [InlineData("MALE", "Male")]
    [InlineData("Female", "Female")]
    [InlineData("f", "Female")]
    [InlineData(" F", "Female")]
    [InlineData("Other", "NotSet")]
    [InlineData("", "NotSet")]
    [InlineData("   ", "NotSet")]
    [InlineData(null, "NotSet")]
    public void Sex_maps(string? g, string expected) => Assert.Equal(expected, DepEdRules.Sex(g));

    // ----- Start year -----

    [Fact]
    public void StartYear_uses_the_row_start_date_when_present()
    {
        // Name says 2026 but the row's start date is 2027 -> the row wins.
        Assert.Equal(2027, DepEdRules.StartYear("2026-2027", Row(2027, 6, 8)));
    }

    [Fact]
    public void StartYear_without_a_row_is_first_four_chars_of_the_name()
    {
        Assert.Equal(2026, DepEdRules.StartYear("2026-2027", null));
    }

    // ----- Age -----
    // First Friday of June 2026 is 5 Jun 2026; of 2025 is 6 Jun 2025; of 2024 is 7 Jun 2024.

    [Fact]
    public void Age_birthday_on_the_first_Friday_itself_counts()
    {
        Assert.Equal(12, DepEdRules.AgeOn(new DateTime(2014, 6, 5), 2026));
    }

    [Fact]
    public void Age_birthday_the_day_after_the_first_Friday_does_not_count_yet()
    {
        Assert.Equal(11, DepEdRules.AgeOn(new DateTime(2014, 6, 6), 2026));
    }

    [Fact]
    public void Age_birthday_the_day_before_the_first_Friday_counts()
    {
        Assert.Equal(12, DepEdRules.AgeOn(new DateTime(2014, 6, 4), 2026));
    }

    [Fact]
    public void Age_first_Friday_is_not_assumed_to_be_the_5th()
    {
        // 2025: first Friday of June is the 6th.
        Assert.Equal(12, DepEdRules.AgeOn(new DateTime(2013, 6, 6), 2025));
        Assert.Equal(11, DepEdRules.AgeOn(new DateTime(2013, 6, 7), 2025));
    }

    [Fact]
    public void Age_when_June_1_is_itself_a_Friday()
    {
        // 1 Jun 2029 is a Friday.
        Assert.Equal(DayOfWeek.Friday, new DateTime(2029, 6, 1).DayOfWeek);
        Assert.Equal(10, DepEdRules.AgeOn(new DateTime(2019, 6, 1), 2029));
        Assert.Equal(9, DepEdRules.AgeOn(new DateTime(2019, 6, 2), 2029));
    }

    [Fact]
    public void Age_feb_29_birth()
    {
        // Born 29 Feb 2016 -> already 10 by 5 Jun 2026.
        Assert.Equal(10, DepEdRules.AgeOn(new DateTime(2016, 2, 29), 2026));
        // Reference 7 Jun 2024 (leap year, Feb 29 already past) -> 8.
        Assert.Equal(8, DepEdRules.AgeOn(new DateTime(2016, 2, 29), 2024));
    }

    [Fact]
    public void Age_with_no_birth_date_is_null()
    {
        Assert.Null(DepEdRules.AgeOn(null, 2026));
        Assert.Null(DepEdRules.AgeOn(default(DateTime), 2026)); // an unset (non-nullable) BirthDate
    }

    // ----- Names -----

    [Fact]
    public void LearnerName_orders_last_first_ext_middle()
    {
        Assert.Equal("Dela Cruz, Juan, Jr., Santos", DepEdRules.LearnerName("Dela Cruz", "Juan", "Jr.", "Santos"));
    }

    [Fact]
    public void LearnerName_skips_blank_extension_and_middle()
    {
        Assert.Equal("Reyes, Ana", DepEdRules.LearnerName("Reyes", "Ana", null, ""));
        Assert.Equal("Reyes, Ana, Lim", DepEdRules.LearnerName("Reyes", "Ana", "  ", "Lim"));
        Assert.Equal("Reyes, Ana, III", DepEdRules.LearnerName("Reyes", "Ana", "III", null));
    }

    [Fact]
    public void ParentName_is_last_first_middle_skipping_blanks()
    {
        Assert.Equal("Santos, Pedro, Cruz", DepEdRules.ParentName("Santos", "Pedro", "Cruz"));
        Assert.Equal("Santos, Pedro", DepEdRules.ParentName("Santos", "Pedro", null));
        Assert.Equal("Santos", DepEdRules.ParentName("Santos", " ", null));
        Assert.Equal("", DepEdRules.ParentName(null, null, null));
    }

    [Fact]
    public void Father_and_mother_names_read_their_own_student_fields()
    {
        var s = new Student
        {
            FatherLastName = "Reyes", FatherFirstName = "Jose", FatherMiddleName = "Lim",
            MotherMaidenLastName = "Garcia", MotherMaidenFirstName = "Maria", MotherMaidenMiddleName = null,
            LastName = "Reyes", // the learner's surname must not leak into the mother's maiden name
        };
        Assert.Equal("Reyes, Jose, Lim", DepEdRules.FatherName(s));
        Assert.Equal("Garcia, Maria", DepEdRules.MotherMaidenName(s));
    }

    // ----- Contact -----

    [Fact]
    public void Contact_prefers_guardian_then_mother_then_father()
    {
        Assert.Equal("G", DepEdRules.Contact("G", "M", "F"));
        Assert.Equal("M", DepEdRules.Contact(null, "M", "F"));
        Assert.Equal("M", DepEdRules.Contact("  ", "M", "F"));
        Assert.Equal("F", DepEdRules.Contact("", " ", "F"));
        Assert.Null(DepEdRules.Contact(null, "", "   "));
    }

    // ----- First attendance / remarks -----

    private static readonly SchoolYear Sy2026 = Row(2026, 6, 8); // starts Mon 8 Jun 2026

    private static Student Plain() => new() { FirstName = "A", LastName = "B" };

    [Fact]
    public void EffectiveFirstAttendance_defaults_to_the_school_year_start_date()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        Assert.Equal(Day(2026, 6, 8), DepEdRules.EffectiveFirstAttendance(e, Sy2026, "2026-2027"));
    }

    [Fact]
    public void EffectiveFirstAttendance_prefers_the_recorded_date_and_drops_the_time()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.FirstAttendanceDate = new DateTime(2026, 7, 13, 9, 30, 0);
        Assert.Equal(Day(2026, 7, 13), DepEdRules.EffectiveFirstAttendance(e, Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_empty_when_nothing_applies()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.LearnerType = "Continuing";
        Assert.Equal("", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_transferee()
    {
        var e = Enr(EnrollmentStatus.Enrolled); e.LearnerType = "Transferee";
        Assert.Equal("T/I", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_returning_balik_aral()
    {
        var e = Enr(EnrollmentStatus.Enrolled); e.LearnerType = "Returning";
        Assert.Equal("B/A", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_late_enrollee_when_first_attendance_is_after_start()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.FirstAttendanceDate = new DateTime(2026, 7, 6);
        Assert.Equal("LE 07/06/2026", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_LE_not_raised_when_first_attendance_equals_start_date()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.FirstAttendanceDate = new DateTime(2026, 6, 8);
        Assert.Equal("", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_LE_not_raised_when_first_attendance_is_before_start_date()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.FirstAttendanceDate = new DateTime(2026, 6, 1);
        Assert.Equal("", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_4Ps_is_CCT()
    {
        var s = Plain(); s.Is4PsBeneficiary = true;
        Assert.Equal("CCT", DepEdRules.Remarks(Enr(EnrollmentStatus.Enrolled), s, Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_disability_is_LWD()
    {
        var s = Plain(); s.HasDisability = true;
        Assert.Equal("LWD", DepEdRules.Remarks(Enr(EnrollmentStatus.Enrolled), s, Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_dropped_is_DRP_with_date()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.WithdrawalType = "Dropped"; e.WithdrawalDate = new DateTime(2026, 9, 3);
        Assert.Equal("DRP 09/03/2026", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_transferred_out_is_TO_with_date_and_school()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.WithdrawalType = "TransferredOut"; e.WithdrawalDate = new DateTime(2026, 9, 3);
        e.TransferredToSchool = "Manila Science HS";
        Assert.Equal("T/O 09/03/2026 Manila Science HS", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_all_codes_in_spec_order()
    {
        // T/I and B/A are mutually exclusive by LearnerType, DRP and T/O by WithdrawalType,
        // so two combinations cover every one of the seven codes in order.
        var s = Plain(); s.Is4PsBeneficiary = true; s.HasDisability = true;

        var a = Enr(EnrollmentStatus.Enrolled);
        a.LearnerType = "Transferee"; a.FirstAttendanceDate = new DateTime(2026, 7, 6);
        a.WithdrawalType = "Dropped"; a.WithdrawalDate = new DateTime(2026, 9, 3);
        Assert.Equal("T/I; LE 07/06/2026; CCT; LWD; DRP 09/03/2026",
            DepEdRules.Remarks(a, s, Sy2026, "2026-2027"));

        var b = Enr(EnrollmentStatus.Enrolled);
        b.LearnerType = "Returning"; b.FirstAttendanceDate = new DateTime(2026, 7, 6);
        b.WithdrawalType = "TransferredOut"; b.WithdrawalDate = new DateTime(2026, 9, 3);
        b.TransferredToSchool = "Manila Science HS";
        Assert.Equal("B/A; LE 07/06/2026; CCT; LWD; T/O 09/03/2026 Manila Science HS",
            DepEdRules.Remarks(b, s, Sy2026, "2026-2027"));
    }

    [Fact]
    public void Remarks_dates_are_culture_invariant()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var e = Enr(EnrollmentStatus.Enrolled);
            e.FirstAttendanceDate = new DateTime(2026, 7, 6);
            Assert.Equal("LE 07/06/2026", DepEdRules.Remarks(e, Plain(), Sy2026, "2026-2027"));
        }
        finally { CultureInfo.CurrentCulture = prev; }
    }

    [Fact]
    public void Remarks_without_a_school_year_row_use_June_1_of_the_name_year_as_start()
    {
        var e = Enr(EnrollmentStatus.Enrolled);
        e.FirstAttendanceDate = new DateTime(2026, 6, 1);
        Assert.Equal("", DepEdRules.Remarks(e, Plain(), null, "2026-2027"));
        e.FirstAttendanceDate = new DateTime(2026, 6, 2);
        Assert.Equal("LE 06/02/2026", DepEdRules.Remarks(e, Plain(), null, "2026-2027"));
    }
}
