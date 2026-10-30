using AssetDesk.Application.Dtos;
using AssetDesk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AssetDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Creates an employee account and returns a JWT.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        auth.RegisterAsync(request, ct);

    /// <summary>Signs in and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        auth.LoginAsync(request, ct);

    /// <summary>Lists users (admin only) so assets can be assigned.</summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public Task<List<UserDto>> Users(CancellationToken ct) => auth.ListUsersAsync(ct);
}
