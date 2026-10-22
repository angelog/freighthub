using Api.Domain.Quotes;
using Api.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Popula regras de frete e histórico de cotações de exemplo quando as tabelas
/// estão vazias — só acontece no primeiro start de uma base nova, para que o
/// portal já mostre dados ao rodar o projeto localmente.
/// </summary>
public sealed class DemoDataSeeder(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow();

        if (!await db.ShippingRules.AnyAsync(cancellationToken))
        {
            db.ShippingRules.AddRange(BuildExampleRules(now));
            logger.LogInformation("Regras de frete de exemplo criadas.");
        }

        if (!await db.ShippingQuoteLogs.AnyAsync(cancellationToken))
        {
            db.ShippingQuoteLogs.AddRange(BuildExampleQuoteLogs(now));
            logger.LogInformation("Histórico de cotações de exemplo criado.");
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IEnumerable<ShippingRule> BuildExampleRules(DateTimeOffset now)
    {
        ShippingRule[] rules =
        [
            new FreeShippingAboveValueRule
            {
                Name = "Frete grátis acima de R$ 300",
                MinimumOrderValue = 300m,
                Priority = 10,
            },
            new MinimumOrderFixedShippingRule
            {
                Name = "Frete fixo R$ 15 acima de R$ 100",
                MinimumOrderValue = 100m,
                FixedPrice = 15m,
                Priority = 20,
            },
            new WeekdayPricingRule
            {
                Name = "Domingo com frete grátis",
                DayOfWeek = DayOfWeek.Sunday,
                IsFree = true,
                Priority = 5,
            },
            new RegionFreeShippingRule
            {
                Name = "Frete grátis região Sudeste (exemplo)",
                CepRanges = [new CepRange("01000000", "09999999")],
                Priority = 15,
            },
        ];

        foreach (var rule in rules)
        {
            rule.CreatedAt = now;
            rule.UpdatedAt = now;
        }

        return rules;
    }

    private static IEnumerable<ShippingQuoteLog> BuildExampleQuoteLogs(DateTimeOffset now) =>
    [
        new ShippingQuoteLog
        {
            CreatedAt = now.AddHours(-1),
            Source = "webhook",
            DestinationZipCode = "01310100",
            CartAmount = 350.00m,
            RequestBody = """{"zipcode":"01310100","cart_amount":350.00}""",
            ResponseBody = """{"quotes":[{"name":"Frete grátis","price":0,"days":5}]}""",
            DurationMs = 42,
            RuleDecided = true,
            CarrierQuoteCount = 0,
            UsedFallback = false,
            SignatureValid = true,
        },
        new ShippingQuoteLog
        {
            CreatedAt = now.AddHours(-5),
            Source = "simulate",
            DestinationZipCode = "20040020",
            CartAmount = 80.00m,
            RequestBody = """{"zipcode":"20040020","cart_amount":80.00}""",
            ResponseBody = """{"quotes":[{"name":"Frete fixo","price":15,"days":4}]}""",
            DurationMs = 18,
            RuleDecided = true,
            CarrierQuoteCount = 0,
            UsedFallback = false,
            SignatureValid = true,
        },
        new ShippingQuoteLog
        {
            CreatedAt = now.AddDays(-1),
            Source = "webhook",
            DestinationZipCode = "30130010",
            CartAmount = 450.00m,
            RequestBody = """{"zipcode":"30130010","cart_amount":450.00}""",
            ResponseBody = """{"quotes":[{"name":"PAC","price":32.5,"days":7},{"name":"SEDEX","price":48.9,"days":3}]}""",
            DurationMs = 980,
            RuleDecided = false,
            CarrierQuoteCount = 2,
            UsedFallback = false,
            SignatureValid = true,
        },
        new ShippingQuoteLog
        {
            CreatedAt = now.AddDays(-2),
            Source = "webhook",
            DestinationZipCode = "70040010",
            CartAmount = 60.00m,
            RequestBody = """{"zipcode":"70040010","cart_amount":60.00}""",
            ResponseBody = """{"quotes":[{"name":"Frete padrão","price":49.9,"days":7}]}""",
            DurationMs = 3510,
            RuleDecided = false,
            CarrierQuoteCount = 0,
            UsedFallback = true,
            SignatureValid = true,
        },
        new ShippingQuoteLog
        {
            CreatedAt = now.AddDays(-3),
            Source = "simulate",
            DestinationZipCode = "90010030",
            CartAmount = 120.00m,
            RequestBody = """{"zipcode":"90010030","cart_amount":120.00}""",
            ResponseBody = """{"quotes":[{"name":"Frete fixo","price":15,"days":4}]}""",
            DurationMs = 21,
            RuleDecided = true,
            CarrierQuoteCount = 0,
            UsedFallback = false,
            SignatureValid = true,
        },
    ];
}
