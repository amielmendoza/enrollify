namespace Enrollify.Domain.Enums;

public enum UserRole
{
    Admin = 0,
    Registrar = 1,
    Parent = 2,
    Student = 3,
    /// <summary>
    /// Cross-tenant operator. Manages the list of tenants (schools) themselves.
    /// Lives outside any single tenant — typically the platform operator.
    /// </summary>
    SuperAdmin = 4
}
