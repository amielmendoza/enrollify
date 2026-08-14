namespace Enrollify.Application.DTOs.ApplicationFormFields;

public record ApplicationFormFieldDto(
    Guid Id,
    string FieldKey,
    string Label,
    string FieldType,
    string Section,
    string AppliesTo,
    bool IsRequired,
    bool IsVisible,
    bool IsBuiltIn,
    bool IsCore,
    int DisplayOrder,
    string? Options,
    string? HelpText);

/// <summary>Body for creating a custom field. Built-in fields are seeded; admins cannot create them.</summary>
public record CreateApplicationFormFieldRequest(
    string FieldKey,
    string Label,
    string FieldType,
    string Section,
    string AppliesTo,
    bool IsRequired,
    bool IsVisible,
    int DisplayOrder,
    string? Options,
    string? HelpText);

/// <summary>
/// Body for updating any field. For built-ins, the server ignores FieldKey, FieldType, Section, AppliesTo
/// and only applies Label / IsRequired / IsVisible / DisplayOrder / Options / HelpText.
/// </summary>
public record UpdateApplicationFormFieldRequest(
    string FieldKey,
    string Label,
    string FieldType,
    string Section,
    string AppliesTo,
    bool IsRequired,
    bool IsVisible,
    int DisplayOrder,
    string? Options,
    string? HelpText);
