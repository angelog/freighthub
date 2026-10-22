using System.Text;
using Api.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Api.Infrastructure.Auth;

public static class AuthDependencyInjection
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(section);
        var options = section.Get<JwtOptions>() ?? new JwtOptions();

        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Hash de senha PBKDF2 (Identity), sem puxar todo o Identity.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        var signingKey = string.IsNullOrWhiteSpace(options.SigningKey)
            ? new byte[32] // placeholder inválido; token service valida e falha claro se usado
            : Encoding.UTF8.GetBytes(options.SigningKey);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
