namespace Api.Infrastructure.Auth;

/// <summary>
/// Usuário admin criado automaticamente quando a tabela de usuários está vazia
/// (seção "Auth:InitialAdmin"). Em produção, defina via variáveis de ambiente.
/// </summary>
public sealed class InitialAdminOptions
{
    public const string SectionName = "Auth:InitialAdmin";

    public string Name { get; set; } = "Administrador";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
