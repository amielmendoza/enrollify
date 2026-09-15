using Enrollify.Application.Features.Auth;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// CWE-640 reset-link-poisoning guard: an attacker-supplied Origin header must never
/// become the base of a genuine password-reset email.
/// </summary>
public class ResetLinkBaseUrlTests
{
    private const string ApiHost = "https://api.enrollify.ph";
    private static readonly string[] Allowed = { "https://app.enrollify.ph", "http://localhost:4200" };

    [Fact]
    public void ConfiguredBaseUrl_AlwaysWins()
    {
        var result = ResetLinkBaseUrl.Resolve("https://portal.enrollify.ph", "https://evil.example", Allowed, ApiHost);

        Assert.Equal("https://portal.enrollify.ph", result);
    }

    [Fact]
    public void AllowlistedOrigin_IsUsed_WhenNoConfiguredBaseUrl()
    {
        var result = ResetLinkBaseUrl.Resolve(null, "https://app.enrollify.ph", Allowed, ApiHost);

        Assert.Equal("https://app.enrollify.ph", result);
    }

    [Theory]
    [InlineData("https://evil.example")]                 // the attack
    [InlineData("https://app.enrollify.ph.evil.example")] // suffix trick
    [InlineData("https://app.enrollify.ph:8443")]        // different port ≠ same origin
    public void UnlistedOrigin_FallsBackToApiHost(string attackerOrigin)
    {
        var result = ResetLinkBaseUrl.Resolve("", attackerOrigin, Allowed, ApiHost);

        Assert.Equal(ApiHost, result);
    }

    [Fact]
    public void MissingOrigin_FallsBackToApiHost()
    {
        Assert.Equal(ApiHost, ResetLinkBaseUrl.Resolve(null, null, Allowed, ApiHost));
    }

    [Fact]
    public void NullAllowlist_NeverTrustsOrigin()
    {
        Assert.Equal(ApiHost, ResetLinkBaseUrl.Resolve(null, "https://app.enrollify.ph", null, ApiHost));
    }

    [Fact]
    public void OriginMatch_IsCaseInsensitive_AndTolerantOfTrailingSlashInConfig()
    {
        var allowed = new[] { "https://App.Enrollify.ph/" };

        var result = ResetLinkBaseUrl.Resolve(null, "https://app.enrollify.ph", allowed, ApiHost);

        Assert.Equal("https://app.enrollify.ph", result);
    }
}
