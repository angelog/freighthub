using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Motor de regras de frete. Avalia as regras ativas em ordem decrescente de
/// prioridade (regras pontuais &gt; regras de data &gt; regras fixas gerais) e
/// permite que cada regra sobrescreva ou combine o resultado da anterior.
/// A primeira decisão terminal (StopProcessing) encerra a avaliação.
/// </summary>
public sealed class RuleEngine
{
    /// <summary>
    /// Avalia as regras contra o contexto. Retorna a decisão final e a trilha
    /// das regras aplicadas (para auditoria e para o simulador do portal).
    /// </summary>
    public RuleEvaluation Evaluate(IEnumerable<ShippingRule> rules, ShippingContext context)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(context);

        var ordered = rules
            .Where(rule => rule.IsActive)
            .OrderByDescending(rule => rule.Priority)
            // desempate estável quando prioridades são iguais
            .ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Id);

        var applied = new List<AppliedRule>();
        var result = RuleResult.Undecided;

        foreach (var rule in ordered)
        {
            if (!rule.AppliesTo(context))
                continue;

            result = rule.Apply(context, result);
            applied.Add(new AppliedRule(
                rule.Id,
                rule.Name,
                rule.GetType().Name,
                rule.Priority,
                result));

            if (result.StopProcessing)
                break;
        }

        return new RuleEvaluation(result, applied);
    }
}
