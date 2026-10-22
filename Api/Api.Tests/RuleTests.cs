using Api.Domain.Rules;

namespace Api.Tests;

public class RuleTests
{
    // ---- FreeShippingAboveValueRule ----

    [Theory]
    [InlineData(100, 150, true)]
    [InlineData(100, 100, true)]  // limite é inclusivo
    [InlineData(100, 99.99, false)]
    public void FreeShippingAboveValue_applies_when_cart_reaches_threshold(
        decimal threshold, decimal cartValue, bool expected)
    {
        var rule = new FreeShippingAboveValueRule { MinimumOrderValue = threshold };
        var ctx = TestSupport.Context(cartValue: cartValue);

        Assert.Equal(expected, rule.AppliesTo(ctx));
    }

    [Fact]
    public void FreeShippingAboveValue_apply_returns_free()
    {
        var rule = new FreeShippingAboveValueRule { MinimumOrderValue = 100 };
        var result = rule.Apply(TestSupport.Context(cartValue: 200), RuleResult.Undecided);

        Assert.True(result.IsDecided);
        Assert.True(result.IsFreeShipping);
        Assert.Equal(0m, result.Price);
        Assert.True(result.StopProcessing);
    }

    // ---- MinimumOrderFixedShippingRule ----

    [Fact]
    public void MinimumOrderFixed_applies_and_returns_fixed_price()
    {
        var rule = new MinimumOrderFixedShippingRule { MinimumOrderValue = 100, FixedPrice = 9.90m };
        var ctx = TestSupport.Context(cartValue: 120);

        Assert.True(rule.AppliesTo(ctx));
        var result = rule.Apply(ctx, RuleResult.Undecided);
        Assert.False(result.IsFreeShipping);
        Assert.Equal(9.90m, result.Price);
    }

    [Fact]
    public void MinimumOrderFixed_does_not_apply_below_threshold()
    {
        var rule = new MinimumOrderFixedShippingRule { MinimumOrderValue = 100, FixedPrice = 9.90m };
        Assert.False(rule.AppliesTo(TestSupport.Context(cartValue: 80)));
    }

    // ---- MaximumOrderFixedShippingRule ----

    [Theory]
    [InlineData(100, 80, true)]    // abaixo do máximo → aplica
    [InlineData(100, 99.99, true)]
    [InlineData(100, 100, false)]  // no limite → não aplica (exclusivo)
    [InlineData(100, 120, false)]  // acima → não aplica
    public void MaximumOrderFixed_applies_when_cart_is_below_threshold(
        decimal max, decimal cartValue, bool expected)
    {
        var rule = new MaximumOrderFixedShippingRule { MaximumOrderValue = max, FixedPrice = 15m };
        Assert.Equal(expected, rule.AppliesTo(TestSupport.Context(cartValue: cartValue)));
    }

    [Fact]
    public void MaximumOrderFixed_returns_fixed_price()
    {
        var rule = new MaximumOrderFixedShippingRule { MaximumOrderValue = 100, FixedPrice = 15m };
        var result = rule.Apply(TestSupport.Context(cartValue: 50), RuleResult.Undecided);

        Assert.False(result.IsFreeShipping);
        Assert.Equal(15m, result.Price);
    }

    // ---- WeekdayPricingRule ----

    [Fact]
    public void Weekday_free_on_sunday_applies_only_on_sunday()
    {
        var rule = new WeekdayPricingRule { DayOfWeek = DayOfWeek.Sunday, IsFree = true };

        Assert.True(rule.AppliesTo(TestSupport.Context(date: TestSupport.Sunday)));
        Assert.False(rule.AppliesTo(TestSupport.Context(date: TestSupport.Monday)));

        var result = rule.Apply(TestSupport.Context(date: TestSupport.Sunday), RuleResult.Undecided);
        Assert.True(result.IsFreeShipping);
    }

    [Fact]
    public void Weekday_with_price_returns_fixed_value()
    {
        var rule = new WeekdayPricingRule { DayOfWeek = DayOfWeek.Monday, IsFree = false, Price = 14.50m };
        var result = rule.Apply(TestSupport.Context(date: TestSupport.Monday), RuleResult.Undecided);

        Assert.False(result.IsFreeShipping);
        Assert.Equal(14.50m, result.Price);
    }

    // ---- RegionFreeShippingRule ----

    [Theory]
    [InlineData("01000000", "01999999", "01001000", true)]  // dentro
    [InlineData("01000000", "01999999", "01000000", true)]  // limite inferior
    [InlineData("01000000", "01999999", "01999999", true)]  // limite superior
    [InlineData("01000000", "01999999", "02000000", false)] // fora
    public void RegionFree_matches_cep_range(string start, string end, string cep, bool expected)
    {
        var rule = new RegionFreeShippingRule { CepRanges = [new CepRange(start, end)] };
        var ctx = TestSupport.Context(cep: cep);

        Assert.Equal(expected, rule.AppliesTo(ctx));
    }

    [Fact]
    public void RegionFree_matches_any_of_multiple_ranges()
    {
        var rule = new RegionFreeShippingRule
        {
            CepRanges =
            [
                new CepRange("01000000", "01999999"),
                new CepRange("90000000", "90999999")
            ]
        };

        Assert.True(rule.AppliesTo(TestSupport.Context(cep: "90000500")));
        Assert.False(rule.AppliesTo(TestSupport.Context(cep: "50000000")));
    }

    // ---- SpecificDateRule ----

    [Fact]
    public void SpecificDate_applies_only_on_that_date()
    {
        var rule = new SpecificDateRule { Date = TestSupport.BlackFriday, IsFree = true };

        Assert.True(rule.AppliesTo(TestSupport.Context(date: TestSupport.BlackFriday)));
        Assert.False(rule.AppliesTo(TestSupport.Context(date: TestSupport.Monday)));
    }

    // ---- DateRangeRule ----

    [Theory]
    [InlineData("2026-11-23", "2026-11-27", "2026-11-25", true)]
    [InlineData("2026-11-23", "2026-11-27", "2026-11-23", true)]  // início inclusivo
    [InlineData("2026-11-23", "2026-11-27", "2026-11-27", true)]  // fim inclusivo
    [InlineData("2026-11-23", "2026-11-27", "2026-11-28", false)] // fora
    public void DateRange_applies_within_inclusive_interval(string start, string end, string day, bool expected)
    {
        var rule = new DateRangeRule
        {
            StartDate = DateOnly.Parse(start),
            EndDate = DateOnly.Parse(end),
            IsFree = true
        };
        var ctx = TestSupport.Context(date: DateOnly.Parse(day));

        Assert.Equal(expected, rule.AppliesTo(ctx));
    }
}
