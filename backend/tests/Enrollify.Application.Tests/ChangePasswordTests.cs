using Enrollify.Application.Features.Auth;
using Enrollify.Application.Features.Auth.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Xunit;

namespace Enrollify.Application.Tests;

public class ChangePasswordTests
{
    [Fact]
    public async Task ChangePassword_WrongCurrent_Fails()
    {
        var ctx = TestDb.Create();
        var user = new User
        {
            Email = "staff@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Right1!"),
            FirstName = "Staff", LastName = "User", Role = UserRole.Registrar, IsActive = true
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new ChangePasswordCommandHandler(ctx)
            .Handle(new ChangePasswordCommand(user.Id, "Wrong1!", "NewPass1!"), default));

        Assert.Equal("Current password is incorrect.", ex.Message);
    }

    [Fact]
    public async Task ChangePassword_HappyPath_ClearsFlag_AndRevokesActiveRefreshTokens()
    {
        var ctx = TestDb.Create();
        var user = new User
        {
            Email = "staff@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Temp1!"),
            FirstName = "Staff", LastName = "User", Role = UserRole.Registrar, IsActive = true,
            MustChangePassword = true
        };
        ctx.Users.Add(user);
        var (active1, _) = AuthTokens.IssueRefreshToken(user.Id);
        var (active2, _) = AuthTokens.IssueRefreshToken(user.Id);
        ctx.RefreshTokens.AddRange(active1, active2);
        await ctx.SaveChangesAsync();

        await new ChangePasswordCommandHandler(ctx)
            .Handle(new ChangePasswordCommand(user.Id, "Temp1!", "MyOwn1!"), default);

        Assert.True(BCrypt.Net.BCrypt.Verify("MyOwn1!", user.PasswordHash));
        Assert.False(user.MustChangePassword);
        Assert.NotNull(active1.RevokedAt);
        Assert.NotNull(active2.RevokedAt);
    }
}
