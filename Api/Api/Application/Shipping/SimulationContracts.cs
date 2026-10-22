namespace Api.Application.Shipping;

/// <summary>Item do carrinho na requisição de simulação.</summary>
public sealed record SimulateItemRequest(
    decimal UnitPrice,
    int Quantity,
    decimal WeightKg,
    decimal? LengthCm = null,
    decimal? WidthCm = null,
    decimal? HeightCm = null,
    string? Sku = null);

/// <summary>Entrada da simulação de frete (mesmos dados do webhook da Yampi).</summary>
public sealed record SimulateRequest
{
    public required string DestinationZipCode { get; init; }

    /// <summary>Momento de referência (default: agora). Usado pelas regras de data/dia.</summary>
    public DateTimeOffset? RequestedAt { get; init; }

    public required IReadOnlyList<SimulateItemRequest> Items { get; init; }
}

/// <summary>Regra que se aplicou durante a avaliação (para explicar a decisão).</summary>
public sealed record AppliedRuleDto(
    Guid RuleId,
    string RuleName,
    string RuleType,
    int Priority,
    bool IsDecided,
    bool IsFreeShipping,
    decimal? Price);

/// <summary>Cotação bruta devolvida por uma transportadora.</summary>
public sealed record CarrierQuoteDto(string Carrier, string ServiceName, decimal Price, int EstimatedDays);

/// <summary>Opção final de frete apresentada (origem: regra ou transportadora).</summary>
public sealed record ShippingOptionDto(string Source, string Name, decimal Price, int? EstimatedDays, bool IsFree);

/// <summary>Resultado completo da simulação.</summary>
public sealed record SimulateResponse
{
    public required string DestinationZipCode { get; init; }
    public required decimal CartTotal { get; init; }
    public required decimal TotalWeightKg { get; init; }

    /// <summary>Se alguma regra interna decidiu o frete.</summary>
    public bool RuleDecided { get; init; }
    public AppliedRuleDto? DecidingRule { get; init; }
    public required IReadOnlyList<AppliedRuleDto> AppliedRules { get; init; }

    /// <summary>Cotações das transportadoras (para transparência/auditoria).</summary>
    public required IReadOnlyList<CarrierQuoteDto> CarrierQuotes { get; init; }

    /// <summary>Opções finais: a decisão da regra, se houver; senão, as cotações.</summary>
    public required IReadOnlyList<ShippingOptionDto> FinalOptions { get; init; }
}
