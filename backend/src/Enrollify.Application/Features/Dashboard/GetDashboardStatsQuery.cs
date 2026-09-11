using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Dashboard;

/// <summary>
/// Dashboard stats, scoped to one school year. When <paramref name="SchoolYear"/> is null the
/// tenant's active school year is used; the resolved year is echoed back in the DTO so the UI
/// can label it. TotalStudents is deliberately year-independent (all active students).
/// If the tenant has no active year and none is given, counts fall back to all years.
/// </summary>
public record GetDashboardStatsQuery(string? SchoolYear = null) : IRequest<DashboardStatsDto>;

public record DashboardStatsDto(
    int TotalStudents,
    int TotalEnrollments,
    int PendingApplications,
    int DraftEnrollments,
    int ApprovedEnrollments,
    int EnrolledCount,
    int TotalSections,
    decimal TotalRevenue,
    int PendingPayments,
    string? SchoolYear);

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetDashboardStatsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var schoolYear = request.SchoolYear;
        if (string.IsNullOrWhiteSpace(schoolYear))
        {
            schoolYear = await _context.SchoolYears
                .Where(sy => sy.IsActive)
                .Select(sy => sy.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var enrollments = _context.Enrollments.AsQueryable();
        var sections = _context.Sections.AsQueryable();
        var payments = _context.Payments.AsQueryable();

        if (schoolYear != null)
        {
            enrollments = enrollments.Where(e => e.SchoolYear == schoolYear);
            sections = sections.Where(s => s.SchoolYear == schoolYear);
            payments = payments.Where(p => p.Enrollment.SchoolYear == schoolYear);
        }

        var totalStudents = await _context.Students.CountAsync(cancellationToken);
        var totalEnrollments = await enrollments.CountAsync(cancellationToken);
        var pendingApps = await _context.AdmissionApplications.CountAsync(a => a.Status == "Submitted", cancellationToken);
        var draftEnrollments = await enrollments.CountAsync(e => e.Status == EnrollmentStatus.Draft || e.Status == EnrollmentStatus.Submitted, cancellationToken);
        var approvedEnrollments = await enrollments.CountAsync(e => e.Status == EnrollmentStatus.Approved, cancellationToken);
        var enrolledCount = await enrollments.CountAsync(e => e.Status == EnrollmentStatus.Enrolled, cancellationToken);
        var totalSections = await sections.CountAsync(s => s.IsActive, cancellationToken);
        var totalRevenue = await payments.Where(p => p.Status == "Approved").SumAsync(p => p.Amount, cancellationToken);
        var pendingPayments = await payments.CountAsync(p => p.Status == "Pending", cancellationToken);

        return new DashboardStatsDto(totalStudents, totalEnrollments, pendingApps, draftEnrollments,
            approvedEnrollments, enrolledCount, totalSections, totalRevenue, pendingPayments, schoolYear);
    }
}
