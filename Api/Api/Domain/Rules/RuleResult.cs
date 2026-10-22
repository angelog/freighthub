namespace Api.Domain.Rules;

/// <summary>
/// Resultado da avaliação de uma (ou da cadeia de) regra(s) de frete.
/// Imutável: cada regra recebe o resultado corrente e devolve um novo.
/// </summary>
public sealed record RuleResult
{
    /// <summary>Indica se alguma regra chegou a uma decisão de frete.</summary>
    public bool IsDecided { get; init; }

    /// <summary>True quando o frete foi definido como grátis.</summary>
    public bool IsFreeShipping { get; init; }

    /// <summary>Valor do frete decidido (0 quando grátis; null quando indeciso).</summary>
    public decimal? Price { get; init; }

    /// <summary>Prazo estimado em dias, quando a regra o define.</summary>
    public int? EstimatedDays { get; init; }

    /// <summary>
    /// Quando true, o motor para de avaliar regras de prioridade menor —
    /// a decisão é terminal.
    /// </summary>
    public bool StopProcessing { get; init; }

    /// <summary>Estado inicial: nenhuma regra decidiu ainda.</summary>
    public static readonly RuleResult Undecided = new();

    /// <summary>Frete grátis.</summary>
    public static RuleResult FreeShipping(int? estimatedDays = null, bool stopProcessing = true) => new()
    {
        IsDecided = true,
        IsFreeShipping = true,
        Price = 0m,
        EstimatedDays = estimatedDays,
        StopProcessing = stopProcessing
    };

    /// <summary>Frete com valor fixo.</summary>
    public static RuleResult FixedPrice(decimal price, int? estimatedDays = null, bool stopProcessing = true)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Valor de frete não pode ser negativo.");
        return new RuleResult
        {
            IsDecided = true,
            IsFreeShipping = false,
            Price = price,
            EstimatedDays = estimatedDays,
            StopProcessing = stopProcessing
        };
    }
}
