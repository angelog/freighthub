namespace Api.Application.Rules;

public sealed record CepRangeDto(string Start, string End);

/// <summary>
/// DTO unificado de regra (entrada e saída). O campo <see cref="Type"/> discrimina
/// o tipo; os campos específicos são preenchidos conforme o tipo.
/// Tipos: free_above_value, min_order_fixed, weekday, region_free, specific_date, date_range.
/// </summary>
public sealed record ShippingRuleDto
{
    public Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;

    // free_above_value / min_order_fixed
    public decimal? MinimumOrderValue { get; init; }
    // max_order_fixed
    public decimal? MaximumOrderValue { get; init; }
    // min_order_fixed / max_order_fixed
    public decimal? FixedPrice { get; init; }

    // weekday / specific_date / date_range
    public bool? IsFree { get; init; }
    public decimal? Price { get; init; }

    // weekday
    public DayOfWeek? DayOfWeek { get; init; }

    // specific_date
    public DateOnly? Date { get; init; }

    // date_range
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }

    // region_free
    public IReadOnlyList<CepRangeDto>? CepRanges { get; init; }
}

public sealed record SetRuleActiveRequest(bool IsActive);
