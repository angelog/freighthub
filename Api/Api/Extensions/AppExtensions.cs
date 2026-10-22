using Api.Application.Rules;
using Api.Application.Shipping;
using Api.Application.Users;
using Api.Application.Yampi;
using Api.Infrastructure;
using Api.Infrastructure.Auth;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Quotes;
using Api.Infrastructure.Yampi;

namespace Api.Extensions;

public static class BuilderExtensions
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);

        // CORS para o portal (Vue) — origens permitidas via config.
        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (corsOrigins.Length > 0)
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        }));

        services.AddScoped<IShippingSimulator, ShippingSimulator>();
        services.AddScoped<IRuleService, RuleService>();

        // Webhook da Yampi.
        services.Configure<YampiOptions>(configuration.GetSection(YampiOptions.SectionName));
        services.AddScoped<IYampiSignatureValidator, HmacYampiSignatureValidator>();
        services.AddScoped<IShippingQuoteService, ShippingQuoteService>();
        services.AddHostedService<QuoteLogRetentionService>();

        // Autenticação do portal (JWT) + usuários.
        services.AddAuthenticationServices(configuration);
        services.Configure<InitialAdminOptions>(configuration.GetSection(InitialAdminOptions.SectionName));
        services.AddScoped<IUserService, UserService>();
        services.AddHostedService<AdminSeeder>();
        services.AddHostedService<DemoDataSeeder>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");
    }
}
