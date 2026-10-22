using Api.Domain.Users;
using Api.Infrastructure.Auth;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Users;

public sealed class UserService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    TimeProvider timeProvider) : IUserService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
            return null;

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        var (token, expiresAt) = tokenService.CreateToken(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return false;

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
            return false;

        SetPassword(user, request.NewPassword);
        user.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new InvalidOperationException("E-mail já cadastrado.");

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        SetPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(
        Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return null;

        var email = Normalize(request.Email);
        if (email != user.Email && await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new InvalidOperationException("E-mail já cadastrado.");

        user.Name = request.Name.Trim();
        user.Email = email;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
            SetPassword(user, request.Password);
        user.UpdatedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    private void SetPassword(User user, string password) =>
        user.PasswordHash = passwordHasher.HashPassword(user, password);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static UserDto ToDto(User u) => new(u.Id, u.Name, u.Email, u.IsActive, u.CreatedAt);
}
