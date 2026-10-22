namespace Api.Domain.Shipping;

/// <summary>
/// Dimensões físicas de um item/pacote em centímetros.
/// </summary>
public sealed record PackageDimensions(decimal LengthCm, decimal WidthCm, decimal HeightCm);

/// <summary>
/// Um item do carrinho, com preço unitário, quantidade, peso e (opcionalmente) dimensões.
/// </summary>
public sealed class CartItem
{
    public CartItem(
        decimal unitPrice, int quantity, decimal weightKg,
        PackageDimensions? dimensions = null, string? sku = null)
    {
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (weightKg < 0) throw new ArgumentOutOfRangeException(nameof(weightKg));

        UnitPrice = unitPrice;
        Quantity = quantity;
        WeightKg = weightKg;
        Dimensions = dimensions;
        Sku = sku;
    }

    public decimal UnitPrice { get; }
    public int Quantity { get; }
    public decimal WeightKg { get; }
    public PackageDimensions? Dimensions { get; }

    /// <summary>SKU do item, quando disponível (usado por transportadoras que cotam por item, ex.: Fretops).</summary>
    public string? Sku { get; }

    public decimal TotalPrice => UnitPrice * Quantity;
    public decimal TotalWeightKg => WeightKg * Quantity;
}

/// <summary>
/// Carrinho de compras submetido para cotação de frete.
/// </summary>
public sealed class Cart
{
    public Cart(IEnumerable<CartItem> items)
    {
        Items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
        if (Items.Count == 0)
            throw new ArgumentException("O carrinho deve conter ao menos um item.", nameof(items));
    }

    public IReadOnlyList<CartItem> Items { get; }

    /// <summary>Valor total das mercadorias (soma de preço x quantidade).</summary>
    public decimal TotalValue => Items.Sum(i => i.TotalPrice);

    /// <summary>Peso total do carrinho em quilos.</summary>
    public decimal TotalWeightKg => Items.Sum(i => i.TotalWeightKg);
}
