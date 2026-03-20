namespace Enrollify.Application.DTOs.Payments;

public record PaymentDto(
    Guid Id,
    Guid EnrollmentId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks,
    DateTime PaymentDate);

public record CreatePaymentRequest(
    Guid EnrollmentId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks);

public record BalanceDto(
    decimal TotalFees,
    decimal TotalPaid,
    decimal Balance);

public record StudentPayRequest(
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks);
