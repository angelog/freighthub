using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Frete com valor fixo quando o carrinho está ABAIXO de um valor.
/// Ex.: "abaixo de R$ 100, frete fixo de R$ 15,00".
/// </summary>
public sealed class MaximumOrderFixedShippingRule : ShippingRule
{
    /// <summary>Valor máximo do carrinho (exclusivo) para o frete fixo valer.</summary>
    public decimal MaximumOrderValue { get; set; }

    /// <summary>Valor fixo de frete aplicado.</summary>
    public decimal FixedPrice { get; set; }

    public override bool AppliesTo(ShippingContext context) =>
        context.Cart.TotalValue < MaximumOrderValue;

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        RuleResult.FixedPrice(FixedPrice);
}
