using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Api.Infrastructure.Carriers;

/// <summary>
/// Pipeline de resiliência aplicado aos HttpClients das transportadoras.
/// Estratégia (por tentativa e no total): timeout curto, retry com backoff
/// exponencial e circuit breaker — para não segurar o checkout da Yampi quando
/// uma transportadora está lenta ou fora do ar.
/// </summary>
public static class CarrierResilience
{
    public static void Configure(HttpStandardResilienceOptions options)
    {
        // Timeout por tentativa (o webhook, na Fase 3, terá um fallback global além disto).
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);

        // 2 tentativas extras com backoff exponencial + jitter.
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);

        // Circuit breaker: abre se muitas chamadas falharem numa janela.
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(15);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);

        // O timeout total precisa acomodar as tentativas + backoff.
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
    }

    /// <summary>
    /// Perfil específico da Fretops: orçamento curto e fixo, praticamente sem
    /// retry. A Yampi só espera 4s no total pela resposta do webhook, e o
    /// orçamento de todas as transportadoras juntas (Yampi:CarrierTimeoutMs) é
    /// 3500ms — como <see cref="CarrierQuoteAggregator"/> usa Task.WhenAll, a
    /// transportadora mais lenta dita o tempo de resposta de TODAS as cotações,
    /// não só das dela. A Fretops responde em ~1,1s "quente"; retries (como no
    /// perfil padrão) só multiplicariam esse tempo sem necessariamente ajudar.
    /// <see cref="HttpStandardResilienceOptions"/> exige MaxRetryAttempts >= 1,
    /// então mantemos 1 tentativa extra mas com TotalRequestTimeout igual ao
    /// AttemptTimeout — na prática a retry nunca tem janela de tempo pra
    /// acontecer, garantindo que uma Fretops lenta não estoure o orçamento das
    /// outras transportadoras.
    /// </summary>
    public static void ConfigureFretops(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1.8);
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.Zero;

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(15);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);

        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(1.8);
    }
}
