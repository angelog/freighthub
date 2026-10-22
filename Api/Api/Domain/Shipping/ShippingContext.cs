namespace Api.Domain.Shipping;

/// <summary>
/// Contexto de uma cotação de frete: tudo o que as regras precisam avaliar.
/// É a entrada tanto do webhook da Yampi quanto do simulador do portal.
/// </summary>
public sealed class ShippingContext
{
    /// <param name="destinationZipCode">CEP de destino (aceita com ou sem hífen).</param>
    /// <param name="cart">Carrinho com itens, valores e pesos.</param>
    /// <param name="requestedAt">
    /// Momento de referência da cotação, no fuso da loja. Usado pelas regras de
    /// dia da semana e de data (dia/semana são derivados deste valor).
    /// </param>
    public ShippingContext(string destinationZipCode, Cart cart, DateTimeOffset requestedAt)
    {
        DestinationZipCode = Cep.Normalize(destinationZipCode);
        Cart = cart ?? throw new ArgumentNullException(nameof(cart));
        RequestedAt = requestedAt;
    }

    /// <summary>CEP de destino normalizado (8 dígitos).</summary>
    public string DestinationZipCode { get; }

    public Cart Cart { get; }

    public DateTimeOffset RequestedAt { get; }

    /// <summary>Dia da semana do momento da cotação.</summary>
    public DayOfWeek DayOfWeek => RequestedAt.DayOfWeek;

    /// <summary>Data (sem hora) do momento da cotação.</summary>
    public DateOnly Date => DateOnly.FromDateTime(RequestedAt.DateTime);
}
