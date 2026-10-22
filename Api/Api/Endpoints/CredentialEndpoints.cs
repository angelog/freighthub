using Api.Domain.Integrations;
using Api.Infrastructure.Integrations;

namespace Api.Endpoints;

/// <summary>
/// Endpoints administrativos de credenciais de integração (exigem JWT do portal).
/// </summary>
public static class CredentialEndpoints
{
    public sealed record SetCredentialRequest(
        IntegrationType Integration,
        IntegrationEnvironment Environment,
        string Key,
        string Value);

    public static void MapCredentialEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/credentials").WithTags("Admin: Credentials").RequireAuthorization();

        // Lista os metadados das credenciais (nunca o valor).
        group.MapGet("/", async (IIntegrationCredentialStore store, CancellationToken ct) =>
            Results.Ok(await store.ListAsync(ct)))
            .WithName("ListCredentials");

        // Valor decifrado de uma credencial (para a tela de edição). ⚠️ expõe o segredo
        // ao admin autenticado — usar só no portal.
        group.MapGet("/{integration}/{environment}/{key}/value", async (
            IntegrationType integration,
            IntegrationEnvironment environment,
            string key,
            IIntegrationCredentialStore store,
            CancellationToken ct) =>
        {
            var value = await store.GetValueAsync(integration, environment, key, ct);
            return value is null ? Results.NotFound() : Results.Ok(new { value });
        })
        .WithName("GetCredentialValue");

        // Remove uma credencial.
        group.MapDelete("/{integration}/{environment}/{key}", async (
            IntegrationType integration,
            IntegrationEnvironment environment,
            string key,
            IIntegrationCredentialStore store,
            CancellationToken ct) =>
            await store.DeleteAsync(integration, environment, key, ct) ? Results.NoContent() : Results.NotFound())
            .WithName("DeleteCredential");

        // Cria/atualiza uma credencial (o valor é cifrado antes de persistir).
        group.MapPut("/", async (
            SetCredentialRequest request,
            IIntegrationCredentialStore store,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["value"] = ["Key e Value são obrigatórios."]
                });

            await store.SetAsync(
                request.Integration, request.Environment, request.Key, request.Value, cancellationToken);

            return Results.NoContent();
        })
        .WithName("SetCredential")
        .WithSummary("Cadastra/atualiza uma credencial de integração (dev-only).");
    }
}
