using System.Text.Json.Serialization;
using Api.Endpoints;
using Api.Extensions;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddServices(builder.Configuration);

// Enums serializados/aceitos como string (ex.: "Loggi", "Production").
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FreightHub API v1");
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Públicos.
app.MapHealthChecks("/health");
app.MapWebhookEndpoints();
app.MapYampiWebhookEndpoints();          // autenticado por HMAC da Yampi
app.MapAuthEndpoints();                   // /login é público; /me e /change-password exigem JWT

// Portal admin (exigem JWT).
app.MapShippingEndpoints();               // simulador
app.MapUserEndpoints();
app.MapRuleEndpoints();
app.MapQuoteLogEndpoints();
app.MapCredentialEndpoints();
app.MapCarrierAdminEndpoints();

app.Run();
