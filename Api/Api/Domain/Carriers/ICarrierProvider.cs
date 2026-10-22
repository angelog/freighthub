namespace Api.Domain.Carriers;

/// <summary>
/// Abstração comum a todas as transportadoras. Cada implementação sabe cotar
/// preço/prazo para um pedido. O webhook consulta os providers habilitados e
/// agrega as opções retornadas.
/// </summary>
public interface ICarrierProvider
{
    /// <summary>Qual transportadora este provider representa.</summary>
    CarrierType Carrier { get; }

    /// <summary>
    /// Cota preço e prazo para o pedido. Uma transportadora pode devolver mais de
    /// uma opção de serviço (ex.: Correios SEDEX e PAC). Deve devolver lista vazia
    /// (não lançar) quando não houver serviço aplicável ao destino.
    /// </summary>
    Task<IReadOnlyList<CarrierQuote>> GetQuotesAsync(
        CarrierQuoteRequest request,
        CancellationToken cancellationToken = default);
}
