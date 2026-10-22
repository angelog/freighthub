using Api.Domain.Rules;

namespace Api.Tests;

public class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    [Fact]
    public void No_applicable_rules_returns_undecided()
    {
        var rules = new[]
        {
            new StubRule(10, applies: false, _ => RuleResult.FreeShipping())
        };

        var evaluation = _engine.Evaluate(rules, TestSupport.Context());

        Assert.False(evaluation.HasDecision);
        Assert.Empty(evaluation.AppliedRules);
        Assert.Null(evaluation.DecidingRule);
    }

    [Fact]
    public void Empty_rule_set_returns_undecided()
    {
        var evaluation = _engine.Evaluate([], TestSupport.Context());

        Assert.False(evaluation.HasDecision);
        Assert.Same(RuleResult.Undecided, evaluation.Result);
    }

    [Fact]
    public void Higher_priority_rule_wins_and_short_circuits()
    {
        var high = new StubRule(100, applies: true, _ => RuleResult.FixedPrice(20m), name: "high");
        var low = new StubRule(10, applies: true, _ => RuleResult.FreeShipping(), name: "low");

        // ordem de entrada invertida de propósito — o motor deve reordenar
        var evaluation = _engine.Evaluate([low, high], TestSupport.Context());

        Assert.Equal(20m, evaluation.Result.Price);
        Assert.Single(evaluation.AppliedRules);
        Assert.Equal("high", evaluation.DecidingRule!.RuleName);
        Assert.Equal(0, low.TimesApplied); // curto-circuito não avaliou a de menor prioridade
    }

    [Fact]
    public void Inactive_rules_are_ignored()
    {
        var inactive = new StubRule(100, applies: true, _ => RuleResult.FixedPrice(99m), name: "inactive", active: false);
        var active = new StubRule(10, applies: true, _ => RuleResult.FreeShipping(), name: "active");

        var evaluation = _engine.Evaluate([inactive, active], TestSupport.Context());

        Assert.True(evaluation.Result.IsFreeShipping);
        Assert.Equal("active", evaluation.DecidingRule!.RuleName);
    }

    [Fact]
    public void Non_stopping_rule_lets_lower_priority_rule_override_and_sees_current()
    {
        RuleResult? seenByLow = null;

        var high = new StubRule(100, applies: true,
            _ => RuleResult.FixedPrice(30m, stopProcessing: false), name: "high");

        var low = new StubRule(10, applies: true, current =>
        {
            seenByLow = current;               // recebe o resultado da regra anterior
            return RuleResult.FreeShipping();  // sobrescreve
        }, name: "low");

        var evaluation = _engine.Evaluate([high, low], TestSupport.Context());

        Assert.True(evaluation.Result.IsFreeShipping);
        Assert.Equal(2, evaluation.AppliedRules.Count);
        Assert.Equal("low", evaluation.DecidingRule!.RuleName);
        Assert.NotNull(seenByLow);
        Assert.Equal(30m, seenByLow!.Price); // enxergou o valor fixo definido pela regra anterior
    }

    [Fact]
    public void Equal_priority_uses_stable_deterministic_order()
    {
        var a = new StubRule(50, applies: true, _ => RuleResult.FixedPrice(1m, stopProcessing: false), name: "aaa");
        var b = new StubRule(50, applies: true, _ => RuleResult.FixedPrice(2m, stopProcessing: false), name: "bbb");

        var e1 = _engine.Evaluate([b, a], TestSupport.Context());
        var e2 = _engine.Evaluate([a, b], TestSupport.Context());

        // desempate por nome: "aaa" antes de "bbb", independentemente da ordem de entrada
        Assert.Equal("aaa", e1.AppliedRules[0].RuleName);
        Assert.Equal("aaa", e2.AppliedRules[0].RuleName);
    }

    [Fact]
    public void Trace_records_rule_type_name()
    {
        var rule = new FreeShippingAboveValueRule { MinimumOrderValue = 50, Name = "Grátis acima de 50" };
        var evaluation = _engine.Evaluate([rule], TestSupport.Context(cartValue: 100));

        var applied = Assert.Single(evaluation.AppliedRules);
        Assert.Equal(nameof(FreeShippingAboveValueRule), applied.RuleType);
        Assert.Equal("Grátis acima de 50", applied.RuleName);
    }

    [Fact]
    public void Pontual_rule_overrides_general_fixed_rule_by_priority()
    {
        // regra fixa geral (baixa prioridade) e promoção de Black Friday (alta prioridade)
        var general = new MinimumOrderFixedShippingRule
        {
            MinimumOrderValue = 0, FixedPrice = 25m, Priority = 10, Name = "Frete padrão"
        };
        var blackFriday = new SpecificDateRule
        {
            Date = TestSupport.BlackFriday, IsFree = true, Priority = 100, Name = "Black Friday"
        };

        var evaluation = _engine.Evaluate(
            [general, blackFriday],
            TestSupport.Context(date: TestSupport.BlackFriday));

        Assert.True(evaluation.Result.IsFreeShipping);
        Assert.Equal("Black Friday", evaluation.DecidingRule!.RuleName);
    }
}
