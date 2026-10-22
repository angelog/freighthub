namespace Api.Application.Users;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Name, string Email, bool IsActive, DateTimeOffset CreatedAt);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

public sealed record CreateUserRequest(string Name, string Email, string Password);

/// <summary>Atualiza dados do usuário; <see cref="Password"/> opcional (redefinição pelo admin).</summary>
public sealed record UpdateUserRequest(string Name, string Email, bool IsActive, string? Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
