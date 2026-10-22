namespace Api.Domain.Carriers;

/// <summary>Transportadora que originou uma cotação.</summary>
public enum CarrierType
{
    Correios = 1,
    Loggi = 2,
    Fretops = 3
}

/// <summary>
/// Pedido de cotação enviado a uma transportadora. Deriva do carrinho + CEPs.
/// </summary>
public sealed record CarrierQuoteRequest
{
    public required string OriginZipCode { get; init; }
    public required string DestinationZipCode { get; init; }

    /// <summary>Peso total em quilos.</summary>
    public required decimal TotalWeightKg { get; init; }

    /// <summary>Valor total das mercadorias (para seguro/declaração).</summary>
    public required decimal DeclaredValue { get; init; }

    // Dimensões agregadas do pacote (opcionais; melhoram a precisão da cotação).
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }

    /// <summary>
    /// Itens do carrinho por SKU. Opcional — Correios/Loggi cotam pelo pacote
    /// agregado (peso/dimensões acima); a Fretops exige itens individuais.
    /// </summary>
    public IReadOnlyList<CarrierQuoteItem>? Items { get; init; }
}

/// <summary>Um item do carrinho, para transportadoras que cotam por SKU (ex.: Fretops).</summary>
public sealed record CarrierQuoteItem
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }

    /// <summary>Peso unitário em gramas.</summary>
    public required int WeightG { get; init; }

    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
}

/// <summary>
/// Uma opção de frete devolvida por uma transportadora.
/// </summary>
public sealed record CarrierQuote
{
    public required CarrierType Carrier { get; init; }

    /// <summary>Nome do serviço (ex.: "SEDEX", "PAC", "Loggi Expresso").</summary>
    public required string ServiceName { get; init; }

    /// <summary>Código do serviço na API da transportadora (ex.: "03220" nos Correios).</summary>
    public string? ExternalServiceCode { get; init; }

    public required decimal Price { get; init; }

    /// <summary>Prazo estimado de entrega em dias úteis.</summary>
    public required int EstimatedDays { get; init; }

    /// <summary>
    /// Id de cotação já atribuído pela transportadora (ex.: <c>checkout_ref</c> da
    /// Fretops). Quando presente, precisa ser devolvido à Yampi como <c>quote_id</c>
    /// sem alteração — é o valor que a transportadora usa para achar a cotação
    /// quando o pedido é pago. Correios/Loggi não têm esse conceito (fica null).
    /// </summary>
    public long? ExternalQuoteId { get; init; }
}
