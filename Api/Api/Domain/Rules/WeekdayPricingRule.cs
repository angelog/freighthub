using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Preço de frete diferenciado por dia da semana. Uma instância cobre um único dia.
/// Cobre também o caso "domingo com frete grátis" (IsFree = true), bastando dar
/// a essa regra uma prioridade alta.
/// </summary>
public sealed class WeekdayPricingRule : ShippingRule
{
    /// <summary>Dia da semana ao qual esta regra se aplica.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Quando true, o frete é grátis nesse dia.</summary>
    public bool IsFree { get; set; }

    /// <summary>Valor de frete no dia (ignorado quando IsFree = true).</summary>
    public decimal? Price { get; set; }

    public override bool AppliesTo(ShippingContext context) =>
        context.DayOfWeek == DayOfWeek;

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        IsFree
            ? RuleResult.FreeShipping()
            : RuleResult.FixedPrice(Price ?? 0m);
}
