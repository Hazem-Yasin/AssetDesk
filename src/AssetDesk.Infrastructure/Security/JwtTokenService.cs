using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssetDesk.Application.Abstractions;
using AssetDesk.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AssetDesk.Infrastructure.Security;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "AssetDesk";
    public string Audience { get; set; } = "AssetDesk";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _o = options.Value;

    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expires = clock.UtcNow.AddMinutes(_o.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, expires: expires, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
