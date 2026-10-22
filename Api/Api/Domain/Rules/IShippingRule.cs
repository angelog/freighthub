using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Contrato de uma regra de frete. Cada regra sabe se se aplica a um contexto
/// e como transformar o resultado corrente.
/// </summary>
public interface IShippingRule
{
    /// <summary>
    /// Prioridade de avaliação. O motor avalia da MAIOR para a menor prioridade;
    /// a primeira regra que produzir uma decisão terminal (StopProcessing) vence.
    /// </summary>
    int Priority { get; }

    /// <summary>Indica se a regra é aplicável ao contexto informado.</summary>
    bool AppliesTo(ShippingContext context);

    /// <summary>
    /// Aplica a regra sobre o resultado corrente, devolvendo um novo resultado.
    /// Recebe <paramref name="current"/> para permitir que regras combinem/ajustem
    /// o que uma regra anterior já decidiu.
    /// </summary>
    RuleResult Apply(ShippingContext context, RuleResult current);
}
