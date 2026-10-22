using Api.Domain.Rules;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Rules;

public sealed class RuleService(AppDbContext db, TimeProvider timeProvider) : IRuleService
{
    public async Task<IReadOnlyList<ShippingRuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rules = await db.ShippingRules
            .AsNoTracking()
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

        return rules.Select(ToDto).ToList();
    }

    public async Task<ShippingRuleDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await db.ShippingRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return rule is null ? null : ToDto(rule);
    }

    public async Task<ShippingRuleDto> CreateAsync(ShippingRuleDto dto, CancellationToken cancellationToken = default)
    {
        var rule = FromDto(dto);
        rule.Id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        rule.CreatedAt = now;
        rule.UpdatedAt = now;

        db.ShippingRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task<ShippingRuleDto?> UpdateAsync(Guid id, ShippingRuleDto dto, CancellationToken cancellationToken = default)
    {
        var rule = await db.ShippingRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
            return null;

        if (!TypeOf(rule).Equals(dto.Type, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Não é possível trocar o tipo de uma regra existente. Exclua e crie outra.");

        rule.Name = dto.Name;
        rule.Priority = dto.Priority;
        rule.IsActive = dto.IsActive;
        rule.UpdatedAt = timeProvider.GetUtcNow();
        ApplyTypeSpecific(rule, dto);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var updated = await db.ShippingRules
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsActive, isActive)
                .SetProperty(r => r.UpdatedAt, timeProvider.GetUtcNow()), cancellationToken);
        return updated > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await db.ShippingRules.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    // ---- mapeamento ----

    private static string TypeOf(ShippingRule rule) => rule switch
    {
        FreeShippingAboveValueRule => "free_above_value",
        MinimumOrderFixedShippingRule => "min_order_fixed",
        MaximumOrderFixedShippingRule => "max_order_fixed",
        WeekdayPricingRule => "weekday",
        RegionFreeShippingRule => "region_free",
        SpecificDateRule => "specific_date",
        DateRangeRule => "date_range",
        _ => "unknown"
    };

    private static ShippingRule FromDto(ShippingRuleDto dto)
    {
        ShippingRule rule = dto.Type switch
        {
            "free_above_value" => new FreeShippingAboveValueRule(),
            "min_order_fixed" => new MinimumOrderFixedShippingRule(),
            "max_order_fixed" => new MaximumOrderFixedShippingRule(),
            "weekday" => new WeekdayPricingRule(),
            "region_free" => new RegionFreeShippingRule(),
            "specific_date" => new SpecificDateRule(),
            "date_range" => new DateRangeRule(),
            _ => throw new ArgumentException($"Tipo de regra inválido: '{dto.Type}'.")
        };

        rule.Name = dto.Name;
        rule.Priority = dto.Priority;
        rule.IsActive = dto.IsActive;
        ApplyTypeSpecific(rule, dto);
        return rule;
    }

    private static void ApplyTypeSpecific(ShippingRule rule, ShippingRuleDto dto)
    {
        switch (rule)
        {
            case FreeShippingAboveValueRule r:
                r.MinimumOrderValue = dto.MinimumOrderValue ?? 0m;
                break;
            case MinimumOrderFixedShippingRule r:
                r.MinimumOrderValue = dto.MinimumOrderValue ?? 0m;
                r.FixedPrice = dto.FixedPrice ?? 0m;
                break;
            case MaximumOrderFixedShippingRule r:
                r.MaximumOrderValue = dto.MaximumOrderValue ?? 0m;
                r.FixedPrice = dto.FixedPrice ?? 0m;
                break;
            case WeekdayPricingRule r:
                r.DayOfWeek = dto.DayOfWeek ?? System.DayOfWeek.Sunday;
                r.IsFree = dto.IsFree ?? false;
                r.Price = dto.Price;
                break;
            case RegionFreeShippingRule r:
                r.CepRanges = (dto.CepRanges ?? [])
                    .Select(c => new CepRange(c.Start, c.End))
                    .ToList();
                break;
            case SpecificDateRule r:
                r.Date = dto.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
                r.IsFree = dto.IsFree ?? false;
                r.Price = dto.Price;
                break;
            case DateRangeRule r:
                r.StartDate = dto.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                r.EndDate = dto.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                r.IsFree = dto.IsFree ?? false;
                r.Price = dto.Price;
                break;
        }
    }

    private static ShippingRuleDto ToDto(ShippingRule rule)
    {
        var dto = new ShippingRuleDto
        {
            Id = rule.Id,
            Type = TypeOf(rule),
            Name = rule.Name,
            Priority = rule.Priority,
            IsActive = rule.IsActive
        };

        return rule switch
        {
            FreeShippingAboveValueRule r => dto with { MinimumOrderValue = r.MinimumOrderValue },
            MinimumOrderFixedShippingRule r => dto with { MinimumOrderValue = r.MinimumOrderValue, FixedPrice = r.FixedPrice },
            MaximumOrderFixedShippingRule r => dto with { MaximumOrderValue = r.MaximumOrderValue, FixedPrice = r.FixedPrice },
            WeekdayPricingRule r => dto with { DayOfWeek = r.DayOfWeek, IsFree = r.IsFree, Price = r.Price },
            RegionFreeShippingRule r => dto with { CepRanges = r.CepRanges.Select(c => new CepRangeDto(c.Start, c.End)).ToList() },
            SpecificDateRule r => dto with { Date = r.Date, IsFree = r.IsFree, Price = r.Price },
            DateRangeRule r => dto with { StartDate = r.StartDate, EndDate = r.EndDate, IsFree = r.IsFree, Price = r.Price },
            _ => dto
        };
    }
}
