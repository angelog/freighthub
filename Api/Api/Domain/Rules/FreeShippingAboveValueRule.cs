using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Frete grátis quando o valor do carrinho atinge (ou supera) um mínimo configurável.
/// </summary>
public sealed class FreeShippingAboveValueRule : ShippingRule
{
    /// <summary>Valor mínimo do carrinho para conceder frete grátis.</summary>
    public decimal MinimumOrderValue { get; set; }

    public override bool AppliesTo(ShippingContext context) =>
        context.Cart.TotalValue >= MinimumOrderValue;

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        RuleResult.FreeShipping();
}
