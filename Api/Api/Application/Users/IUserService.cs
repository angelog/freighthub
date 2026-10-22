namespace Api.Application.Users;

public interface IUserService
{
    /// <summary>Autentica e devolve o token; null se credenciais inválidas ou usuário inativo.</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Troca a própria senha; false se a senha atual não confere.</summary>
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<UserDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cria um usuário. Lança InvalidOperationException se o e-mail já existir.</summary>
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atualiza um usuário (e opcionalmente redefine a senha). Null se não existir.</summary>
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
