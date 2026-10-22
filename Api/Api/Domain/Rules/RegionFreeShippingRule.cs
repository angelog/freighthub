using Api.Domain.Shipping;

namespace Api.Domain.Rules;

/// <summary>
/// Uma faixa de CEP (início/fim, inclusivos), armazenada como 8 dígitos.
/// </summary>
public sealed class CepRange
{
    public CepRange() { }

    public CepRange(string start, string end)
    {
        Start = Cep.Normalize(start);
        End = Cep.Normalize(end);
    }

    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;

    /// <summary>
    /// Verifica se o CEP (já normalizado) cai dentro da faixa. Como todos têm
    /// 8 dígitos, a comparação ordinal equivale à numérica.
    /// </summary>
    public bool Contains(string normalizedCep) =>
        string.CompareOrdinal(Start, normalizedCep) <= 0 &&
        string.CompareOrdinal(normalizedCep, End) <= 0;
}

/// <summary>
/// Frete grátis para destinos dentro de uma ou mais faixas de CEP.
/// </summary>
public sealed class RegionFreeShippingRule : ShippingRule
{
    /// <summary>Faixas de CEP contempladas com frete grátis.</summary>
    public List<CepRange> CepRanges { get; set; } = [];

    public override bool AppliesTo(ShippingContext context) =>
        CepRanges.Any(range => range.Contains(context.DestinationZipCode));

    public override RuleResult Apply(ShippingContext context, RuleResult current) =>
        RuleResult.FreeShipping();
}
