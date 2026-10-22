using System.Security.Claims;
using Api.Application.Users;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        // Login (público).
        group.MapPost("/login", async (
            LoginRequest request,
            IUserService users,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["credentials"] = ["Informe e-mail e senha."]
                });

            var response = await users.LoginAsync(request, cancellationToken);
            return response is null
                ? Results.Problem("Credenciais inválidas.", statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(response);
        })
        .WithName("Login")
        .WithSummary("Autentica um usuário e retorna o token JWT.");

        // Dados do usuário logado.
        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IUserService users,
            CancellationToken cancellationToken) =>
        {
            var id = GetUserId(principal);
            if (id is null)
                return Results.Unauthorized();

            var user = await users.GetAsync(id.Value, cancellationToken);
            return user is null ? Results.Unauthorized() : Results.Ok(user);
        })
        .RequireAuthorization()
        .WithName("Me")
        .WithSummary("Retorna o usuário autenticado.");

        // Troca da própria senha.
        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            ClaimsPrincipal principal,
            IUserService users,
            CancellationToken cancellationToken) =>
        {
            var id = GetUserId(principal);
            if (id is null)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["newPassword"] = ["A nova senha deve ter ao menos 8 caracteres."]
                });

            var ok = await users.ChangePasswordAsync(id.Value, request, cancellationToken);
            return ok
                ? Results.NoContent()
                : Results.Problem("Senha atual incorreta.", statusCode: StatusCodes.Status400BadRequest);
        })
        .RequireAuthorization()
        .WithName("ChangePassword")
        .WithSummary("Troca a senha do usuário autenticado.");
    }

    internal static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
