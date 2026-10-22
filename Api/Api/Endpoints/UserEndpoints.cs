using System.Security.Claims;
using Api.Application.Users;

namespace Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        // Todo o CRUD exige autenticação (sem perfis: qualquer usuário logado gerencia usuários).
        var group = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/", async (IUserService users, CancellationToken ct) =>
            Results.Ok(await users.ListAsync(ct)))
            .WithName("ListUsers");

        group.MapGet("/{id:guid}", async (Guid id, IUserService users, CancellationToken ct) =>
        {
            var user = await users.GetAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        })
        .WithName("GetUser");

        group.MapPost("/", async (CreateUserRequest request, IUserService users, CancellationToken ct) =>
        {
            if (!TryValidate(request.Name, request.Email, request.Password, passwordRequired: true, out var problem))
                return problem;

            try
            {
                var created = await users.CreateAsync(request, ct);
                return Results.Created($"/api/v1/users/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("CreateUser");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, IUserService users, CancellationToken ct) =>
        {
            if (!TryValidate(request.Name, request.Email, request.Password, passwordRequired: false, out var problem))
                return problem;

            try
            {
                var updated = await users.UpdateAsync(id, request, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("UpdateUser");

        group.MapDelete("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, IUserService users, CancellationToken ct) =>
        {
            // Evita o usuário logado se auto-excluir.
            if (AuthEndpoints.GetUserId(principal) == id)
                return Results.Problem("Você não pode excluir o próprio usuário.", statusCode: StatusCodes.Status400BadRequest);

            return await users.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteUser");
    }

    private static bool TryValidate(string name, string email, string? password, bool passwordRequired, out IResult problem)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Nome é obrigatório."];
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) errors["email"] = ["E-mail inválido."];

        // Na criação a senha é obrigatória; na edição, só valida se foi informada (redefinição).
        if (passwordRequired || !string.IsNullOrEmpty(password))
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                errors["password"] = ["Senha deve ter ao menos 8 caracteres."];
        }

        if (errors.Count > 0)
        {
            problem = Results.ValidationProblem(errors);
            return false;
        }

        problem = Results.Empty;
        return true;
    }
}
