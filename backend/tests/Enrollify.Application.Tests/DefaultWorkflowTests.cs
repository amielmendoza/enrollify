using Enrollify.Application.Features.Workflows;
using Enrollify.Domain.Enums;
using Xunit;

namespace Enrollify.Application.Tests;

public class DefaultWorkflowTests
{
    [Theory]
    [InlineData(EnrollmentStatus.Draft, EnrollmentStatus.Submitted)]
    [InlineData(EnrollmentStatus.Submitted, EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Assessed, EnrollmentStatus.Approved)]
    [InlineData(EnrollmentStatus.Approved, EnrollmentStatus.Paid)]
    [InlineData(EnrollmentStatus.Paid, EnrollmentStatus.Enrolled)]
    public void NextStatus_ReturnsDefinedTransition(EnrollmentStatus from, EnrollmentStatus expected)
    {
        Assert.Equal(expected, DefaultWorkflow.NextStatus(from));
    }

    [Theory]
    [InlineData(EnrollmentStatus.Enrolled)]
    [InlineData(EnrollmentStatus.Cancelled)]
    public void NextStatus_TerminalStatuses_ReturnNull(EnrollmentStatus terminal)
    {
        Assert.Null(DefaultWorkflow.NextStatus(terminal));
    }

    [Fact]
    public void Steps_CoverEveryNonTerminalStatusExactlyOnce()
    {
        var froms = DefaultWorkflow.Steps.Select(s => s.From).ToList();
        Assert.Equal(froms.Count, froms.Distinct().Count());
        Assert.Equal(
            new[] { EnrollmentStatus.Draft, EnrollmentStatus.Submitted, EnrollmentStatus.Assessed, EnrollmentStatus.Approved, EnrollmentStatus.Paid },
            froms);
    }
}
