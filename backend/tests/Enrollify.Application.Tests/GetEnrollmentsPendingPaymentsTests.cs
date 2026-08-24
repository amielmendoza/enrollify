using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

public class GetEnrollmentsPendingPaymentsTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment WithPending, Enrollment WithoutPending)> SeedAsync()
    {
        var ctx = TestDb.Create();

        var studentA = new Student { LRN = "LRN-0801", FirstName = "Ines", LastName = "Aquino", Address = "Laguna" };
        var studentB = new Student { LRN = "LRN-0802", FirstName = "Bea", LastName = "Bautista", Address = "Laguna" };
        var withPending = new Enrollment { StudentId = studentA.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Approved };
        var withoutPending = new Enrollment { StudentId = studentB.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Approved };

        ctx.Students.AddRange(studentA, studentB);
        ctx.Enrollments.AddRange(withPending, withoutPending);
        ctx.Payments.AddRange(
            new Payment { EnrollmentId = withPending.Id, Amount = 500m, PaymentMethod = "GCash", Status = "Pending" },
            new Payment { EnrollmentId = withPending.Id, Amount = 700m, PaymentMethod = "Cash", Status = "Pending" },
            new Payment { EnrollmentId = withPending.Id, Amount = 900m, PaymentMethod = "Cash", Status = "Approved" },
            new Payment { EnrollmentId = withoutPending.Id, Amount = 100m, PaymentMethod = "Cash", Status = "Approved" },
            new Payment { EnrollmentId = withoutPending.Id, Amount = 200m, PaymentMethod = "Cash", Status = "Rejected" });
        await ctx.SaveChangesAsync();

        return (ctx, withPending, withoutPending);
    }

    [Fact]
    public async Task Rows_CarryPendingPaymentsCount()
    {
        var (ctx, withPending, withoutPending) = await SeedAsync();

        var result = await new GetEnrollmentsQueryHandler(ctx)
            .Handle(new GetEnrollmentsQuery(null, null, null, null), default);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Single(e => e.Id == withPending.Id).PendingPaymentsCount);
        Assert.Equal(0, result.Items.Single(e => e.Id == withoutPending.Id).PendingPaymentsCount);
    }

    [Fact]
    public async Task PendingPaymentsOnly_FiltersToEnrollmentsWithPendingPayments()
    {
        var (ctx, withPending, _) = await SeedAsync();

        var result = await new GetEnrollmentsQueryHandler(ctx)
            .Handle(new GetEnrollmentsQuery(null, null, null, null, PendingPaymentsOnly: true), default);

        Assert.Equal(1, result.TotalCount);
        var row = Assert.Single(result.Items);
        Assert.Equal(withPending.Id, row.Id);
        Assert.Equal(2, row.PendingPaymentsCount);
    }
}
