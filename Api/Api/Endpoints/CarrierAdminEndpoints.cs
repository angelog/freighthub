using Api.Domain.Carriers;
using Api.Infrastructure.Carriers;

namespace Api.Endpoints;

/// <summary>
/// Endpoints administrativos para ligar/desligar transportadoras em runtime
/// (exigem JWT do portal).
/// </summary>
public static class CarrierAdminEndpoints
{
    public sealed record CarrierStatusDto(string Carrier, bool Available, bool Enabled);

    public sealed record SetCarrierEnabledRequest(bool IsEnabled);

    public static void MapCarrierAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/carriers").WithTags("Admin: Carriers").RequireAuthorization();

        // Lista as transportadoras registradas (disponíveis) e o estado efetivo.
        group.MapGet("/", async (
            IEnumerable<ICarrierProvider> providers,
            ICarrierSettingsStore settings,
            CancellationToken cancellationToken) =>
        {
            var states = await settings.GetEnabledStatesAsync(cancellationToken);
            var available = providers.Select(p => p.Carrier).ToHashSet();

            var list = states
                .Select(kv => new CarrierStatusDto(
                    kv.Key.ToString(),
                    Available: available.Contains(kv.Key),
                    Enabled: kv.Value))
                .OrderBy(x => x.Carrier)
                .ToList();

            return Results.Ok(list);
        })
        .WithName("ListCarriers")
        .WithSummary("Lista as transportadoras e o estado ligado/desligado.");

        // Liga/desliga uma transportadora.
        group.MapPut("/{carrier}", async (
            CarrierType carrier,
            SetCarrierEnabledRequest request,
            ICarrierSettingsStore settings,
            CancellationToken cancellationToken) =>
        {
            await settings.SetEnabledAsync(carrier, request.IsEnabled, cancellationToken);
            return Results.NoContent();
        })
        .WithName("SetCarrierEnabled")
        .WithSummary("Ativa ou desativa uma transportadora em runtime.");
    }
}
