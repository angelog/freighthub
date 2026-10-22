using Api.Application.Rules;

namespace Api.Endpoints;

public static class RuleEndpoints
{
    public static void MapRuleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/rules").WithTags("Rules").RequireAuthorization();

        group.MapGet("/", async (IRuleService rules, CancellationToken ct) =>
            Results.Ok(await rules.ListAsync(ct)))
            .WithName("ListRules");

        group.MapGet("/{id:guid}", async (Guid id, IRuleService rules, CancellationToken ct) =>
        {
            var rule = await rules.GetAsync(id, ct);
            return rule is null ? Results.NotFound() : Results.Ok(rule);
        })
        .WithName("GetRule");

        group.MapPost("/", async (ShippingRuleDto dto, IRuleService rules, CancellationToken ct) =>
        {
            try
            {
                var created = await rules.CreateAsync(dto, ct);
                return Results.Created($"/api/v1/rules/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = [ex.Message] });
            }
        })
        .WithName("CreateRule");

        group.MapPut("/{id:guid}", async (Guid id, ShippingRuleDto dto, IRuleService rules, CancellationToken ct) =>
        {
            try
            {
                var updated = await rules.UpdateAsync(id, dto, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("UpdateRule");

        group.MapPatch("/{id:guid}/active", async (
            Guid id, SetRuleActiveRequest request, IRuleService rules, CancellationToken ct) =>
            await rules.SetActiveAsync(id, request.IsActive, ct) ? Results.NoContent() : Results.NotFound())
            .WithName("SetRuleActive");

        group.MapDelete("/{id:guid}", async (Guid id, IRuleService rules, CancellationToken ct) =>
            await rules.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .WithName("DeleteRule");
    }
}
