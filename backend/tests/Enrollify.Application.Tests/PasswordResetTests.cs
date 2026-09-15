using Enrollify.Application.Features.Auth;
using Enrollify.Application.Features.Auth.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class PasswordResetTests
{
    private const string BaseUrl = "http://localhost:4200";

    private static User NewUser(string email, string password = "OldPass1!", bool mustChange = false) => new()
    {
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        FirstName = "Pat",
        LastName = "Reset",
        Role = UserRole.Parent,
        IsActive = true,
        MustChangePassword = mustChange
    };

    private static string ExtractTokenFromEmail(RecordingEmailSender email)
    {
        var body = Assert.Single(email.Sent).Body;
        var start = body.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var end = body.IndexOfAny(new[] { '\n', '\r', ' ' }, start);
        return body[start..(end < 0 ? body.Length : end)];
    }

    [Fact]
    public async Task Forgot_UnknownEmail_SucceedsSilently_WritesAndSendsNothing()
    {
        var ctx = TestDb.Create();
        var email = new RecordingEmailSender();

        var result = await new ForgotPasswordCommandHandler(ctx, email)
            .Handle(new ForgotPasswordCommand("nobody@example.com", BaseUrl), default);

        Assert.True(result); // indistinguishable from success — no account enumeration
        Assert.Empty(email.Sent);
        Assert.Empty(await ctx.Users.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Forgot_KnownEmail_StoresHashedTokenAndEmailsResetLink()
    {
        var ctx = TestDb.Create();
        var user = NewUser("pat@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var email = new RecordingEmailSender();

        await new ForgotPasswordCommandHandler(ctx, email)
            .Handle(new ForgotPasswordCommand("pat@example.com", BaseUrl), default);

        Assert.NotNull(user.PasswordResetTokenHash);
        Assert.NotNull(user.PasswordResetTokenExpiresAt);
        Assert.InRange(user.PasswordResetTokenExpiresAt!.Value, DateTime.UtcNow.AddMinutes(55), DateTime.UtcNow.AddMinutes(65));

        var sent = Assert.Single(email.Sent);
        Assert.Equal("pat@example.com", sent.To);
        Assert.Contains($"{BaseUrl}/reset-password?email=", sent.Body);

        // Only the HASH is stored; the raw token lives in the link alone.
        var rawToken = ExtractTokenFromEmail(email);
        Assert.NotEqual(rawToken, user.PasswordResetTokenHash);
        Assert.Equal(AuthTokens.Sha256(rawToken), user.PasswordResetTokenHash);
    }

    [Fact]
    public async Task Reset_HappyPath_IsSingleUse_AndRevokesRefreshTokens()
    {
        var ctx = TestDb.Create();
        var user = NewUser("pat@example.com", mustChange: true);
        ctx.Users.Add(user);
        var (refreshToken, _) = AuthTokens.IssueRefreshToken(user.Id);
        ctx.RefreshTokens.Add(refreshToken);
        await ctx.SaveChangesAsync();

        var email = new RecordingEmailSender();
        await new ForgotPasswordCommandHandler(ctx, email)
            .Handle(new ForgotPasswordCommand("pat@example.com", BaseUrl), default);
        var rawToken = ExtractTokenFromEmail(email);

        await new ResetPasswordCommandHandler(ctx)
            .Handle(new ResetPasswordCommand("pat@example.com", rawToken, "NewPass1!"), default);

        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass1!", user.PasswordHash)); // login works with the new password
        Assert.Null(user.PasswordResetTokenHash);            // token cleared
        Assert.Null(user.PasswordResetTokenExpiresAt);
        Assert.False(user.MustChangePassword);
        Assert.NotNull(refreshToken.RevokedAt);              // stolen sessions die

        // Single use: replaying the same token fails.
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ResetPasswordCommandHandler(ctx)
            .Handle(new ResetPasswordCommand("pat@example.com", rawToken, "Another1!"), default));
    }

    [Fact]
    public async Task Reset_ExpiredToken_Fails()
    {
        var ctx = TestDb.Create();
        var user = NewUser("pat@example.com");
        user.PasswordResetTokenHash = AuthTokens.Sha256("expired-token");
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new ResetPasswordCommandHandler(ctx)
            .Handle(new ResetPasswordCommand("pat@example.com", "expired-token", "NewPass1!"), default));

        Assert.Contains("invalid or has expired", ex.Message);
        Assert.True(BCrypt.Net.BCrypt.Verify("OldPass1!", user.PasswordHash)); // password unchanged
    }

    [Fact]
    public async Task Reset_WrongToken_Fails()
    {
        var ctx = TestDb.Create();
        var user = NewUser("pat@example.com");
        user.PasswordResetTokenHash = AuthTokens.Sha256("the-real-token");
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ResetPasswordCommandHandler(ctx)
            .Handle(new ResetPasswordCommand("pat@example.com", "a-guessed-token", "NewPass1!"), default));
    }
}
