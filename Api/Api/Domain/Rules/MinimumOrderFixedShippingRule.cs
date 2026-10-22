using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Frete com valor fixo a partir de um valor mínimo de carrinho.
/// Ex.: "acima de R$ 100, frete fixo de R$ 9,90".
/// </summary>
public sealed class MinimumOrderFixedShippingRule : ShippingRule
{
    /// <summary>Valor mínimo do carrinho para o frete fixo valer.</summary>
    public decimal MinimumOrderValue { get; set; }

    /// <summary>Valor fixo de frete aplicado.</summary>
    public decimal FixedPrice { get; set; }

    public override bool AppliesTo(ShippingContext context) =>
        context.Cart.TotalValue >= MinimumOrderValue;

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        RuleResult.FixedPrice(FixedPrice);
}
