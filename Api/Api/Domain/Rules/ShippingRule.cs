using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Base de uma regra de frete configurável. É ao mesmo tempo a entidade
/// persistida (via herança TPH no EF Core) e o comportamento avaliado pelo motor.
/// Cada tipo concreto (frete grátis por valor, faixa de CEP, data, etc.)
/// carrega seus próprios parâmetros e implementa a lógica de aplicação.
/// </summary>
public abstract class ShippingRule : IShippingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nome legível exibido/gerenciado no portal admin.</summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public int Priority { get; set; }

    /// <summary>Regras inativas são ignoradas pelo motor.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public abstract bool AppliesTo(ShippingContext context);

    public abstract RuleResult Apply(ShippingContext context, RuleResult current);
}
