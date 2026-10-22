using Api.Infrastructure.Persistence;
using Api.Infrastructure.Yampi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Quotes;

/// <summary>
/// Expurga os logs de cotação mais antigos que a janela de retenção (7 dias por
/// padrão). Roda uma vez ao subir e depois a cada 24h. Ver plano §6.1.
/// </summary>
public sealed class QuoteLogRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<YampiOptions> options,
    TimeProvider timeProvider,
    ILogger<QuoteLogRetentionService> logger) : BackgroundService
{
    private readonly int _retentionDays = Math.Max(1, options.Value.LogRetentionDays);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao expurgar logs de cotação.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-_retentionDays);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var removed = await db.ShippingQuoteLogs
            .Where(l => l.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
            logger.LogInformation(
                "Expurgo de logs de cotação: {Removed} registros anteriores a {Cutoff:o} removidos.",
                removed, cutoff);
    }
}
