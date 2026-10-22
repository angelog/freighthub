using System.Text;
using Api.Domain.Users;
using Api.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

public class JwtTokenServiceTests
{
    private const string Key = "IhQs/z7uOqxvoFmQ0LxCHwAvv5oU2xDb3wnW17nXyGGzgnRoO2klpCxyQpBwkNIU";

    private static JwtTokenService Create(string key = Key) => new(
        Options.Create(new JwtOptions { SigningKey = key, Issuer = "freighthub", Audience = "freighthub", ExpiryMinutes = 60 }),
        TimeProvider.System);

    private static TokenValidationParameters Validation(string key = Key) => new()
    {
        ValidIssuer = "freighthub",
        ValidAudience = "freighthub",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };

    [Fact]
    public async Task Creates_valid_token_with_user_claims()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "fulano@example.com", Name = "Fulano" };

        var (token, expiresAt) = Create().CreateToken(user);

        Assert.True(expiresAt > DateTimeOffset.UtcNow);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Validation());
        Assert.True(result.IsValid);
        Assert.Equal(user.Id.ToString(), result.Claims["sub"].ToString());
        Assert.Equal("fulano@example.com", result.Claims["email"].ToString());
    }

    [Fact]
    public void Rejects_short_signing_key()
    {
        var service = Create("chave-curta");
        Assert.Throws<InvalidOperationException>(() =>
            service.CreateToken(new User { Email = "a@b.com", Name = "x" }));
    }

    [Fact]
    public async Task Token_signed_with_other_key_fails_validation()
    {
        var (token, _) = Create().CreateToken(new User { Id = Guid.NewGuid(), Email = "a@b.com", Name = "x" });

        var otherKey = Convert.ToBase64String(new byte[48]); // chave diferente
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Validation(otherKey));

        Assert.False(result.IsValid);
    }
}
