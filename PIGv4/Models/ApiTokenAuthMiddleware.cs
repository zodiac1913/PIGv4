using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace PIGv4.Models;

/// <summary>
/// Middleware that checks for "Authorization: Bearer {token}" header.
/// If valid, sets the user identity so [Authorize] works for API requests.
/// Runs alongside cookie auth — whichever succeeds first wins.
/// </summary>
public class ApiTokenAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ApiTokenAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, PigContext db)
    {
        // Skip if already authenticated (cookie auth worked)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                var apiToken = await db.ApiToken
                    .FirstOrDefaultAsync(t => t.Token == token && t.IsActive);

                if (apiToken != null)
                {
                    var user = await db.AppUser.FindAsync(apiToken.AppUserId);
                    if (user != null && user.IsActive && user.IsApproved)
                    {
                        // Update last used
                        apiToken.LastUsed = DateTime.Now;
                        await db.SaveChangesAsync();

                        // Set identity
                        var claims = new List<Claim>
                        {
                            new(ClaimTypes.Name, user.Username),
                            new(ClaimTypes.Role, user.Role),
                            new("DisplayName", user.DisplayName ?? user.Username),
                            new("UserId", user.AppUserId.ToString())
                        };
                        var identity = new ClaimsIdentity(claims, "ApiToken");
                        context.User = new ClaimsPrincipal(identity);
                    }
                }
            }
        }

        await _next(context);
    }
}
