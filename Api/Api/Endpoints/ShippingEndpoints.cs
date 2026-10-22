using Api.Application.Shipping;

namespace Api.Endpoints;

public static class ShippingEndpoints
{
    public static void MapShippingEndpoints(this WebApplication app)
    {
        // Simulador — ferramenta do portal, exige autenticação.
        var group = app.MapGroup("/api/v1/shipping").WithTags("Shipping").RequireAuthorization();

        group.MapPost("/simulate", async (
            SimulateRequest request,
            IShippingSimulator simulator,
            CancellationToken cancellationToken) =>
        {
            if (request.Items is null || request.Items.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = ["Informe ao menos um item."]
                });

            try
            {
                var result = await simulator.SimulateAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                // CEP inválido, item inválido, etc.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [ex.ParamName ?? "request"] = [ex.Message]
                });
            }
        })
        .WithName("SimulateShipping")
        .WithSummary("Simula o cálculo de frete (regras + transportadoras) em modo dry-run.");
    }
}
