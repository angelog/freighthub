namespace Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var endpoints = app.MapGroup("/api");
        endpoints.MapGet("/", () => Results.Ok());
    }
}