using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Regra pontual para uma data exata (ex.: Black Friday). Por ser uma promoção
/// pontual, costuma receber prioridade alta para sobrepor as regras fixas gerais.
/// </summary>
public sealed class SpecificDateRule : ShippingRule
{
    /// <summary>Data em que a regra vale.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Quando true, o frete é grátis na data.</summary>
    public bool IsFree { get; set; }

    /// <summary>Valor de frete na data (ignorado quando IsFree = true).</summary>
    public decimal? Price { get; set; }

    public override bool AppliesTo(ShippingContext context) =>
        context.Date == Date;

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        IsFree
            ? RuleResult.FreeShipping()
            : RuleResult.FixedPrice(Price ?? 0m);
}
