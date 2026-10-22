using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Regra pontual para um intervalo de datas (início/fim, inclusivos) —
/// ex.: uma campanha de uma semana.
/// </summary>
public sealed class DateRangeRule : ShippingRule
{
    /// <summary>Primeiro dia do intervalo (inclusivo).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Último dia do intervalo (inclusivo).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Quando true, o frete é grátis no intervalo.</summary>
    public bool IsFree { get; set; }

    /// <summary>Valor de frete no intervalo (ignorado quando IsFree = true).</summary>
    public decimal? Price { get; set; }

    public override bool AppliesTo(ShippingContext context) =>
        context.Date >= StartDate && context.Date <= EndDate;

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        IsFree
            ? RuleResult.FreeShipping()
            : RuleResult.FixedPrice(Price ?? 0m);
}
