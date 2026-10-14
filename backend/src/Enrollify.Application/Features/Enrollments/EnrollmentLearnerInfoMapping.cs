using Enrollify.Application.DTOs.Learners;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Enrollments;

public static class EnrollmentLearnerInfoMapping
{
    public static EnrollmentLearnerInfoDto ToDto(Enrollment e) => new(
        e.LearnerType, e.LastGradeLevelCompleted, e.LastSchoolYearCompleted,
        e.LastSchoolAttended, e.LastSchoolAddress, e.LastSchoolId, e.Track, e.Strand);
}
