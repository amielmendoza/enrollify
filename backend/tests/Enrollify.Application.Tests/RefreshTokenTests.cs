using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Auth;
using Enrollify.Application.Features.Auth.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class RefreshTokenTests
{
    private class FakeJwt : IJwtTokenGenerator
    {
        public string GenerateToken(User user) => $"jwt-{user.Id}";
    }

    private static async Task<(ApplicationDbContext Ctx, User User)> SeedUserAsync(bool mustChange = false)
    {
        var ctx = TestDb.Create();
        var user = new User
        {
            Email = "kim@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Secret1!"),
            FirstName = "Kim", LastName = "Session",
            Role = UserRole.Registrar, IsActive = true,
            MustChangePassword = mustChange
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return (ctx, user);
    }

    [Fact]
    public async Task Login_IssuesRefreshToken_AndCarriesMustChangePassword()
    {
        var (ctx, user) = await SeedUserAsync(mustChange: true);

        var response = await new LoginCommandHandler(ctx, new FakeJwt())
            .Handle(new LoginCommand("kim@example.com", "Secret1!"), default);

        Assert.NotNull(response.RefreshToken);
        Assert.True(response.MustChangePassword);

        var row = await ctx.RefreshTokens.SingleAsync();
        Assert.Equal(user.Id, row.UserId);
        Assert.Equal(AuthTokens.Sha256(response.RefreshToken!), row.TokenHash); // only the hash is stored
        Assert.Null(row.RevokedAt);
        Assert.InRange(row.ExpiresAt, DateTime.UtcNow.AddDays(13), DateTime.UtcNow.AddDays(15));
    }

    [Fact]
    public async Task Refresh_Rotates_OldTokenDies_NewTokenWorks()
    {
        var (ctx, user) = await SeedUserAsync();
        var login = await new LoginCommandHandler(ctx, new FakeJwt())
            .Handle(new LoginCommand("kim@example.com", "Secret1!"), default);

        var handler = new RefreshTokenCommandHandler(ctx, new FakeJwt());
        var first = await handler.Handle(new RefreshTokenCommand(login.RefreshToken!), default);

        Assert.NotNull(first.RefreshToken);
        Assert.NotEqual(login.RefreshToken, first.RefreshToken);
        Assert.Equal(user.TenantId, first.TenantId); // same claims context as login

        // The presented (now revoked) token is dead on replay...
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RefreshTokenCommand(login.RefreshToken!), default));

        // ...while the rotated one keeps working.
        var second = await handler.Handle(new RefreshTokenCommand(first.RefreshToken!), default);
        Assert.NotNull(second.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_IsUnauthorized()
    {
        var (ctx, user) = await SeedUserAsync();
        var raw = AuthTokens.GenerateUrlSafeToken(64);
        ctx.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = AuthTokens.Sha256(raw),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new RefreshTokenCommandHandler(ctx, new FakeJwt()).Handle(new RefreshTokenCommand(raw), default));
    }

    [Fact]
    public async Task Refresh_OpportunisticallyDeletesTheUsersExpiredTokens()
    {
        var (ctx, user) = await SeedUserAsync();
        ctx.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = AuthTokens.Sha256("long-dead"),
            ExpiresAt = DateTime.UtcNow.AddDays(-30)
        });
        await ctx.SaveChangesAsync();
        var login = await new LoginCommandHandler(ctx, new FakeJwt())
            .Handle(new LoginCommand("kim@example.com", "Secret1!"), default);

        await new RefreshTokenCommandHandler(ctx, new FakeJwt())
            .Handle(new RefreshTokenCommand(login.RefreshToken!), default);

        var remaining = await ctx.RefreshTokens.ToListAsync();
        Assert.DoesNotContain(remaining, t => t.TokenHash == AuthTokens.Sha256("long-dead")); // purged
        Assert.Equal(2, remaining.Count); // the rotated-out (revoked) token + the fresh one
    }
}
