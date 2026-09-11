using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class CreateEnrollmentTemplateTests
{
    [Fact]
    public async Task CreateEnrollment_SeedsRequirementsFromActiveTemplates_NotHardcodedList()
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-1001", FirstName = "Tess", LastName = "Uy", Address = "Bohol" };
        ctx.Students.Add(student);
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "PSA Birth Certificate", IsActive = true, DisplayOrder = 1 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Form 138", GradeLevel = "Grade 7", IsActive = true, DisplayOrder = 2 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Grade 9 Only", GradeLevel = "Grade 9", IsActive = true, DisplayOrder = 3 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Retired Doc", IsActive = false, DisplayOrder = 4 });
        await ctx.SaveChangesAsync();

        var dto = await new CreateEnrollmentCommandHandler(ctx)
            .Handle(new CreateEnrollmentCommand(student.Id, "2025-2026", "Grade 7"), default);

        var requirements = await ctx.EnrollmentRequirements
            .Where(r => r.EnrollmentId == dto.Id)
            .Select(r => r.DocumentName)
            .ToListAsync();

        Assert.Equal(2, requirements.Count);
        Assert.Contains("PSA Birth Certificate", requirements);
        Assert.Contains("Form 138", requirements);
        Assert.DoesNotContain("Grade 9 Only", requirements);
        Assert.DoesNotContain("Retired Doc", requirements);
        // The old hardcoded defaults are gone unless a template says so.
        Assert.DoesNotContain("Good Moral Certificate", requirements);
    }
}
