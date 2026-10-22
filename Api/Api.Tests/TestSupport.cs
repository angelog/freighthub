using Api.Domain.Rules;
using Api.Domain.Shipping;

namespace Api.Tests;

internal static class TestSupport
{
    // 2026-06-15 é uma segunda-feira; usada como default "dia útil".
    public static readonly DateOnly Monday = new(2026, 6, 15);
    public static readonly DateOnly Sunday = new(2026, 6, 14);
    public static readonly DateOnly BlackFriday = new(2026, 11, 27); // sexta

    public static ShippingContext Context(
        decimal cartValue = 100m,
        string cep = "01001000",
        DateOnly? date = null,
        decimal weightKg = 1m)
    {
        var d = date ?? Monday;
        var when = new DateTimeOffset(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.Zero);
        var cart = new Cart([new CartItem(cartValue, 1, weightKg)]);
        return new ShippingContext(cep, cart, when);
    }
}

/// <summary>
/// Regra de teste com comportamento configurável, para exercitar o RuleEngine
/// independentemente das regras de negócio concretas.
/// </summary>
internal sealed class StubRule : ShippingRule
{
    private readonly Func<RuleResult, RuleResult> _apply;
    private readonly bool _applies;

    public StubRule(
        int priority,
        bool applies,
        Func<RuleResult, RuleResult> apply,
        string name = "stub",
        bool active = true)
    {
        Priority = priority;
        _applies = applies;
        _apply = apply;
        Name = name;
        IsActive = active;
    }

    public int TimesApplied { get; private set; }

    public override bool AppliesTo(ShippingContext context) => _applies;

    public override RuleResult Apply(ShippingContext context, RuleResult current)
    {
        TimesApplied++;
        return _apply(current);
    }
}
