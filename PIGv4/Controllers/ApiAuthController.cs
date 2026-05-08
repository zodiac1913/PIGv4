using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

/// <summary>
/// API authentication for PIG Mobile.
/// POST /api/auth/login — get a token
/// POST /api/auth/revoke — revoke a token (admin)
/// GET /api/auth/tokens — list active tokens (admin)
/// </summary>
[Route("api/auth")]
[ApiController]
public class ApiAuthController : ControllerBase
{
    private readonly PigContext _context;

    public ApiAuthController(PigContext context) => _context = context;

    /// <summary>
    /// Login with username/password, receive an API token.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ApiLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Username and password required." });

        var user = await _context.AppUser.FirstOrDefaultAsync(u => u.Username == req.Username.Trim());
        if (user == null || !VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid username or password." });

        if (!user.IsApproved)
            return Unauthorized(new { error = "Account pending admin approval." });

        if (!user.IsActive)
            return Unauthorized(new { error = "Account deactivated." });

        // Generate token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenStr = Convert.ToHexStringLower(tokenBytes);

        var apiToken = new ApiToken
        {
            AppUserId = user.AppUserId,
            Token = tokenStr,
            DeviceName = req.DeviceName
        };

        _context.ApiToken.Add(apiToken);
        user.LastLogin = DateTime.Now;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            token = tokenStr,
            username = user.Username,
            displayName = user.DisplayName ?? user.Username,
            role = user.Role
        });
    }

    /// <summary>
    /// Revoke a token by ID. Admin only.
    /// </summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeRequest req)
    {
        var token = await _context.ApiToken.FindAsync(req.ApiTokenId);
        if (token == null) return NotFound();
        token.IsActive = false;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// List all active tokens. Admin only.
    /// </summary>
    [HttpGet("tokens")]
    public async Task<IActionResult> Tokens()
    {
        var tokens = await _context.ApiToken
            .Where(t => t.IsActive)
            .Join(_context.AppUser, t => t.AppUserId, u => u.AppUserId, (t, u) => new
            {
                t.ApiTokenId,
                t.DeviceName,
                t.Created,
                t.LastUsed,
                u.Username
            })
            .OrderByDescending(t => t.LastUsed ?? t.Created)
            .ToListAsync();

        return Ok(tokens);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexStringLower(bytes) == hash;
    }
}

public class ApiLoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DeviceName { get; set; }
}

public class RevokeRequest
{
    public int ApiTokenId { get; set; }
}
