using System.Security.Claims;

namespace AssetDesk.Api;

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("Missing user id claim."));

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole("Admin");
}
