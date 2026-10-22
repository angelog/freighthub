using System.Text.Json.Serialization;

namespace Api.Application.Yampi;

/// <summary>Requisição de cálculo de frete enviada pela Yampi (modo "Frete por API").</summary>
public sealed class YampiQuoteRequest
{
    [JsonPropertyName("zipcode")] public string Zipcode { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("cart")] public YampiCart? Cart { get; set; }
    [JsonPropertyName("skus")] public List<YampiSku> Skus { get; set; } = [];
}

public sealed class YampiCart
{
    [JsonPropertyName("promocode")] public string? Promocode { get; set; }
    [JsonPropertyName("customer")] public YampiCustomer? Customer { get; set; }
}

public sealed class YampiCustomer
{
    [JsonPropertyName("document")] public string? Document { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
}

public sealed class YampiSku
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("product_id")] public long ProductId { get; set; }
    [JsonPropertyName("sku")] public string? Sku { get; set; }
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("unit_price")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
    [JsonPropertyName("length")] public decimal Length { get; set; }
    [JsonPropertyName("width")] public decimal Width { get; set; }
    [JsonPropertyName("height")] public decimal Height { get; set; }
    [JsonPropertyName("weight")] public decimal Weight { get; set; }
    [JsonPropertyName("availability_days")] public int AvailabilityDays { get; set; }
}

/// <summary>Resposta esperada pela Yampi.</summary>
public sealed class YampiQuoteResponse
{
    [JsonPropertyName("quotes")] public IReadOnlyList<YampiQuote> Quotes { get; set; } = [];
}

public sealed class YampiQuote
{
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("service")] public required string Service { get; set; }
    [JsonPropertyName("price")] public required decimal Price { get; set; }
    [JsonPropertyName("days")] public required int Days { get; set; }
    [JsonPropertyName("quote_id")] public required long QuoteId { get; set; }
    [JsonPropertyName("free_shipment")] public required bool FreeShipment { get; set; }
}
