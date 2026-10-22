namespace Api.Application.Rules;

public interface IRuleService
{
    Task<IReadOnlyList<ShippingRuleDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ShippingRuleDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShippingRuleDto> CreateAsync(ShippingRuleDto dto, CancellationToken cancellationToken = default);
    Task<ShippingRuleDto?> UpdateAsync(Guid id, ShippingRuleDto dto, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
