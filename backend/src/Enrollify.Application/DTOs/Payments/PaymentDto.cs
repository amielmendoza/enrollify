namespace Enrollify.Application.DTOs.Payments;

public record PaymentDto(
    Guid Id,
    Guid EnrollmentId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks,
    DateTime PaymentDate,
    string Status,
    string? ReviewedBy,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    string? ReceiptFileName = null,
    string? ReceiptFileUrl = null,
    string? VoidedBy = null,
    DateTime? VoidedAt = null,
    string? VoidReason = null);

public record ReviewPaymentRequest(
    bool IsApproved,
    string? Notes);

public record VoidPaymentRequest(
    string Reason);

public record CreatePaymentRequest(
    Guid EnrollmentId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks,
    string? ReceiptFileName = null,
    string? ReceiptFileUrl = null);

public record BalanceDto(
    decimal TotalFees,
    decimal TotalPaid,
    decimal Balance);

public record ParentPayRequest(
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks,
    string? ReceiptFileName = null,
    string? ReceiptFileUrl = null);
