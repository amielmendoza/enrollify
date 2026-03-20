using Enrollify.Domain.Enums;

namespace Enrollify.Application.DTOs.Enrollments;

public record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string SchoolYear,
    string GradeLevel,
    Guid? SectionId,
    string? SectionName,
    EnrollmentStatus Status,
    string? Remarks,
    string? PaymentPlan,
    DateTime CreatedAt,
    List<RequirementDto>? Requirements);

public record RequirementDto(
    Guid Id,
    string DocumentName,
    bool IsSubmitted,
    string? FileName,
    string? Notes,
    bool IsVerified,
    string? VerifiedBy);

public record CreateEnrollmentRequest(
    Guid StudentId,
    string SchoolYear,
    string GradeLevel);

public record MoveEnrollmentStepRequest(
    string? Remarks);

public record AssignSectionRequest(
    Guid SectionId);

public record SubmitRequirementRequest(
    Guid RequirementId,
    string FileName);

public record SelectPaymentPlanRequest(
    string PaymentPlan);
