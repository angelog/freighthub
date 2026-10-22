namespace Api.Application.Shipping;

/// <summary>
/// Executa uma cotação de frete em modo dry-run: roda o motor de regras e as
/// transportadoras, devolvendo o resultado com a explicação de qual regra decidiu.
/// É a base tanto do simulador do portal quanto (depois) do webhook da Yampi.
/// </summary>
public interface IShippingSimulator
{
    Task<SimulateResponse> SimulateAsync(SimulateRequest request, CancellationToken cancellationToken = default);
}
