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
    string? VerifiedBy,
    DateTime? VerifiedAt = null,
    string? ReviewNotes = null);

public record EnrollmentStatusHistoryDto(
    string FromStatus,
    string ToStatus,
    string? Remarks,
    DateTime TransitionDate);

public record LedgerAdjustmentDto(
    Guid Id,
    string Type,
    string Description,
    decimal Amount,
    string PostedBy,
    DateTime CreatedAt);

public record PostAdjustmentRequest(
    string Type,
    string Description,
    decimal Amount);

public record VoidAdjustmentRequest(
    string Reason);

public record AdminUploadRequirementRequest(string FileName, string? FileUrl);

public record ReviewRequirementRequest(bool IsVerified, string? Notes);

public record CreateEnrollmentRequest(
    Guid StudentId,
    string SchoolYear,
    string GradeLevel);

public record MoveEnrollmentStepRequest(
    string? Remarks);

public record CancelEnrollmentRequest(
    string? Reason);

public record AssignSectionRequest(
    Guid SectionId);

public record SubmitRequirementRequest(
    Guid RequirementId,
    string FileName,
    string? FileUrl);

public record SelectPaymentPlanRequest(
    string PaymentPlan);
