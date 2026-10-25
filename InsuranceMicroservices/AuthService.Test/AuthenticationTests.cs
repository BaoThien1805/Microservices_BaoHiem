using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using AuthService.DataAccess;
using AuthService.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Test;
public class AuthenticationTests
{
    private const string Secret = "TH6-test-only-signing-key-at-least-32-bytes";
    private static Domain.AuthService CreateService() => new(new InsuranceAgentsInMemoryDb(),
        Options.Create(new AppSettings { Secret = Secret }));

    [Theory]
    [InlineData("missing", "secret")]
    [InlineData("jimmy.solid", "wrong")]
    [InlineData("", "secret")]
    public void InvalidCredentialsAreRejected(string login, string password) =>
        Assert.Null(CreateService().Authenticate(login, password));

    [Fact]
    public void SuccessfulLoginIssuesSignedTokenWithGatewayAndChatClaims()
    {
        var result = Assert.IsType<AuthResult>(CreateService().Authenticate("jimmy.solid", "secret"));
        var principal = new JwtSecurityTokenHandler().ValidateToken(result.Token, new TokenValidationParameters
        {
            ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateIssuerSigningKey = true
        }, out _);
        Assert.Equal("jimmy.solid", principal.Identity?.Name);
        Assert.True(principal.IsInRole("SALESMAN"));
        Assert.Equal("SALESMAN", principal.FindFirst("userType")?.Value);
        Assert.NotNull(principal.FindFirst("avatar"));
        Assert.True(result.ExpiryTimeStamp > DateTime.UtcNow);
    }

    [Fact]
    public void AgentProfileDoesNotExposePassword()
    {
        var agent = new InsuranceAgentsInMemoryDb().FindByLogin("jimmy.solid")!;
        Assert.NotEqual("secret", agent.Password);
        Assert.True(agent.PasswordMatches("secret"));
        Assert.DoesNotContain("password", JsonSerializer.Serialize(agent).ToLowerInvariant());
    }
}
