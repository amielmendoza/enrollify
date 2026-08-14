using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Dashboard;

public record GetDashboardStatsQuery() : IRequest<DashboardStatsDto>;

public record DashboardStatsDto(
    int TotalStudents,
    int TotalEnrollments,
    int PendingApplications,
    int DraftEnrollments,
    int ApprovedEnrollments,
    int EnrolledCount,
    int TotalSections,
    decimal TotalRevenue,
    int PendingPayments);

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetDashboardStatsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var totalStudents = await _context.Students.CountAsync(cancellationToken);
        var totalEnrollments = await _context.Enrollments.CountAsync(cancellationToken);
        var pendingApps = await _context.AdmissionApplications.CountAsync(a => a.Status == "Submitted", cancellationToken);
        var draftEnrollments = await _context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Draft || e.Status == EnrollmentStatus.Submitted, cancellationToken);
        var approvedEnrollments = await _context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Approved, cancellationToken);
        var enrolledCount = await _context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Enrolled, cancellationToken);
        var totalSections = await _context.Sections.CountAsync(s => s.IsActive, cancellationToken);
        var totalRevenue = await _context.Payments.Where(p => p.Status == "Approved").SumAsync(p => p.Amount, cancellationToken);
        var pendingPayments = await _context.Payments.CountAsync(p => p.Status == "Pending", cancellationToken);

        return new DashboardStatsDto(totalStudents, totalEnrollments, pendingApps, draftEnrollments,
            approvedEnrollments, enrolledCount, totalSections, totalRevenue, pendingPayments);
    }
}
