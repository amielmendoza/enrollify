using Enrollify.Application.DTOs.Students;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Students;

/// <summary>Single Student → StudentDto mapping; new learner fields are added here, nowhere else.</summary>
public static class StudentMapping
{
    public static StudentDto ToDto(Student s) => new(
        s.Id, s.LRN, s.FirstName, s.MiddleName, s.LastName,
        s.BirthDate, s.Gender, s.Address, s.ContactNumber, s.Email,
        s.GuardianName, s.GuardianContact, s.FullName,
        s.UserId, s.ParentUserId, s.IsActive,
        s.StudentNumber, s.LrnNeedsReview, LearnerProfileMapping.ToDto(s));
}
