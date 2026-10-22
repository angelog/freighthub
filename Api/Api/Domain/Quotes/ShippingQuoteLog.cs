namespace Api.Domain.Quotes;

/// <summary>
/// Registro de uma cotação recebida via webhook (ou simulador), para auditoria/debug.
/// Retenção limitada (7 dias por padrão) — ver serviço de expurgo. Não é log permanente.
/// </summary>
public sealed class ShippingQuoteLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Origem do registro: "webhook" ou "simulate".</summary>
    public string Source { get; set; } = "webhook";

    public string DestinationZipCode { get; set; } = string.Empty;
    public decimal CartAmount { get; set; }

    /// <summary>Corpo bruto recebido (JSON).</summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>Resposta devolvida (JSON).</summary>
    public string ResponseBody { get; set; } = string.Empty;

    public int DurationMs { get; set; }
    public bool RuleDecided { get; set; }
    public int CarrierQuoteCount { get; set; }
    public bool UsedFallback { get; set; }
    public bool SignatureValid { get; set; }
}
