using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Interfaces;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Enrollify.Application.Tests;

/// <summary>
/// In-memory IEmailSender fake: records every send so tests can assert notification
/// behavior (and, like the real SmtpEmailSender, never throws).
/// </summary>
public class RecordingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fixed-tenant stub so ApplicationDbContext's global tenant query filter and TenantId
/// auto-assignment have a value in tests.
/// </summary>
public class FixedTenantProvider : ITenantProvider
{
    private Guid _tenantId;

    public FixedTenantProvider(Guid tenantId) => _tenantId = tenantId;

    public Guid GetTenantId() => _tenantId;

    public void SetTenantId(Guid tenantId) => _tenantId = tenantId;
}

public static class TestDb
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// A fresh in-memory ApplicationDbContext scoped to <see cref="TenantId"/> — isolated per
    /// call unless a shared <paramref name="dbName"/> is given (used by tests that verify
    /// persisted state through a second context). Entities saved through it get that TenantId
    /// auto-assigned, so the global tenant filter behaves exactly as in production.
    /// </summary>
    public static ApplicationDbContext Create(string? dbName = null) =>
        new(Options(dbName ?? $"enrollify-tests-{Guid.NewGuid()}"), new FixedTenantProvider(TenantId));

    public static DbContextOptions<ApplicationDbContext> Options(string dbName) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            // The InMemory provider has no real transactions; ExecuteInTransactionAsync's
            // BeginTransaction becomes a no-op instead of throwing.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
}
