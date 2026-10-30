using AssetDesk.Application.Abstractions;
using AssetDesk.Application.Dtos;
using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Enums;
using AssetDesk.Domain.Exceptions;

namespace AssetDesk.Application.Services;

public class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IUnitOfWork uow, IClock clock)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new DomainException("An account with this email already exists.");

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Role = UserRole.Employee, // admins are provisioned by seeding / an existing admin, never self-service
            CreatedAt = clock.UtcNow
        };
        users.Add(user);
        await uow.SaveChangesAsync(ct);
        return Issue(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationFailedException("Invalid email or password.");
        return Issue(user);
    }

    public async Task<List<UserDto>> ListUsersAsync(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(Mapper.ToDto).ToList();

    private AuthResponse Issue(User user)
    {
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, Mapper.ToDto(user));
    }
}
