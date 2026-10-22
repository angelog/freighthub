namespace Api.Domain.Rules;

/// <summary>
/// Registro de uma regra que foi aplicada durante a avaliação — usado para
/// explicar, no simulador do portal, qual(is) regra(s) decidiram o frete.
/// </summary>
public sealed record AppliedRule(
    Guid RuleId,
    string RuleName,
    string RuleType,
    int Priority,
    RuleResult Result);

/// <summary>
/// Resultado completo de uma avaliação do motor: a decisão final e a trilha
/// de regras que se aplicaram, em ordem de avaliação.
/// </summary>
public sealed record RuleEvaluation(RuleResult Result, IReadOnlyList<AppliedRule> AppliedRules)
{
    /// <summary>True quando alguma regra chegou a uma decisão de frete.</summary>
    public bool HasDecision => Result.IsDecided;

    /// <summary>A regra que efetivamente decidiu o frete (a última aplicada), se houver.</summary>
    public AppliedRule? DecidingRule =>
        AppliedRules.Count > 0 ? AppliedRules[^1] : null;

    /// <summary>Avaliação sem nenhuma regra aplicável.</summary>
    public static RuleEvaluation Empty { get; } = new(RuleResult.Undecided, []);
}
