using System.Text.Json;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Admissions;

/// <summary>The one AdmissionApplication → ApplicationDetailDto mapping, and API → command applicant mapping.</summary>
public static class ApplicationMapping
{
    public static ApplicationDetailDto ToDetailDto(AdmissionApplication a) => new(
        a.Id, a.ApplicationNumber, a.FirstName, a.MiddleName, a.LastName,
        a.Email, a.ContactNumber, a.Gender, a.DateOfBirth, a.Address,
        a.GradeLevel, a.SchoolYear, a.PreviousSchool, a.PreviousSchoolAddress,
        a.GuardianName, a.GuardianContact, a.GuardianRelationship,
        a.Status, a.CreatedAt, a.ReviewedAt, a.ReviewNotes, a.StudentId,
        a.ApplicationType, a.ParentFirstName, a.ParentLastName, a.ParentEmail, a.ParentContactNumber,
        ParseCustomFieldValues(a.CustomFieldValues),
        a.Lrn,
        LearnerProfileMapping.ToDto(a),
        new EnrollmentLearnerInfoDto(a.LearnerType, a.LastGradeLevelCompleted, a.LastSchoolYearCompleted,
            a.PreviousSchool, a.PreviousSchoolAddress, a.LastSchoolId, a.Track, a.Strand));

    public static SubmitApplicationCommand.Applicant ToCommandApplicant(ApplicantData a) => new(
        a.FirstName, a.MiddleName, a.LastName,
        a.Email, a.ContactNumber, a.Gender,
        a.DateOfBirth, a.Address,
        a.GradeLevel, a.SchoolYear,
        a.PreviousSchool, a.PreviousSchoolAddress,
        a.GuardianName, a.GuardianContact, a.GuardianRelationship,
        a.CustomFieldValues,
        a.Lrn, a.Learner, a.LearnerType, a.LastGradeLevelCompleted, a.LastSchoolYearCompleted,
        a.LastSchoolId, a.Track, a.Strand);

    public static Dictionary<string, string?>? ParseCustomFieldValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json); }
        catch { return null; } // malformed JSON is ignored, as before
    }
}
