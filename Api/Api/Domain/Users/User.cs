namespace Api.Domain.Users;

/// <summary>
/// Usuário do portal administrativo. Sem perfis/roles — todos têm o mesmo acesso.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>E-mail usado como login (único).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash da senha (PBKDF2, via PasswordHasher).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
