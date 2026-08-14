using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

/// <summary>
/// A field configuration for the public /apply admission form, per tenant.
/// Two flavours:
///  - Built-in fields (IsBuiltIn=true): the /apply form has hardcoded rendering for them; the admin
///    can only toggle visibility/requirement and edit Label / Options. FieldKey and FieldType are locked.
///  - Custom fields (IsBuiltIn=false): added by the admin, fully editable, values stored in
///    AdmissionApplication.CustomFieldValues (JSON map keyed by FieldKey).
/// </summary>
public class ApplicationFormField : TenantEntity
{
    public string FieldKey { get; set; } = default!;       // unique within tenant; e.g. "middleName" or "allergies"
    public string Label { get; set; } = default!;
    public string FieldType { get; set; } = "Text";        // Text | TextArea | Number | Date | Checkbox | Dropdown
    public string Section { get; set; } = "Student";       // Parent | Student | Enrollment | Guardian
    public string AppliesTo { get; set; } = "Both";        // Both | ParentMode | StudentMode
    public bool IsRequired { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsBuiltIn { get; set; }
    /// <summary>
    /// Core fields are part of the data model and can never be hidden or made optional —
    /// the application would break without them. Admins can rename their label / help text
    /// and reorder them within a section, but visibility and requiredness are locked on.
    /// </summary>
    public bool IsCore { get; set; }
    public int DisplayOrder { get; set; }
    public string? Options { get; set; }                   // JSON array of strings, for Dropdown
    public string? HelpText { get; set; }
}
