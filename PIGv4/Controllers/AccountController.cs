using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class AccountController : Controller
{
    private readonly PigContext _context;

    public AccountController(PigContext context) => _context = context;

    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // If no users exist, redirect to setup
        if (!await _context.AppUser.AnyAsync())
            return RedirectToAction("Setup");
        
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Setup()
    {
        // Only allow setup if no users exist
        if (await _context.AppUser.AnyAsync())
            return RedirectToAction("Login");
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Setup(string username, string password, string confirmPassword)
    {
        if (await _context.AppUser.AnyAsync())
            return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username and password required.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        var admin = new AppUser
        {
            Username = username.Trim(),
            PasswordHash = HashPassword(password),
            DisplayName = username.Trim(),
            Role = "Admin",
            IsApproved = true,
            IsActive = true
        };

        _context.AppUser.Add(admin);
        await _context.SaveChangesAsync();

        // Auto-login the new admin
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, admin.Username),
            new(ClaimTypes.Role, admin.Role),
            new("DisplayName", admin.DisplayName ?? admin.Username),
            new("UserId", admin.AppUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return Redirect("/");
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username and password required.";
            return View();
        }

        var user = await _context.AppUser.FirstOrDefaultAsync(u => u.Username == username.Trim());
        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        if (!user.IsApproved)
        {
            ViewBag.Error = "Your account is pending admin approval.";
            return View();
        }

        if (!user.IsActive)
        {
            ViewBag.Error = "Your account has been deactivated.";
            return View();
        }

        user.LastLogin = DateTime.Now;
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("DisplayName", user.DisplayName ?? user.Username),
            new("UserId", user.AppUserId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return Redirect(returnUrl ?? "/");
    }

    [AllowAnonymous]
    public IActionResult Register() => View();

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(string username, string password, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username and password required.";
            return View();
        }

        var exists = await _context.AppUser.AnyAsync(u => u.Username == username.Trim());
        if (exists)
        {
            ViewBag.Error = "Username already taken.";
            return View();
        }

        var user = new AppUser
        {
            Username = username.Trim(),
            PasswordHash = HashPassword(password),
            DisplayName = displayName?.Trim(),
            Role = "User",
            IsApproved = false,
            IsActive = true
        };

        _context.AppUser.Add(user);
        await _context.SaveChangesAsync();

        ViewBag.Success = "Account created! Waiting for admin approval.";
        return View();
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // Admin panel for user management
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users()
    {
        var users = await _context.AppUser.OrderBy(u => u.Username).ToListAsync();
        return View(users);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> ApproveUser(int id)
    {
        var user = await _context.AppUser.FindAsync(id);
        if (user != null) { user.IsApproved = true; await _context.SaveChangesAsync(); }
        return RedirectToAction("Users");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> RejectUser(int id)
    {
        var user = await _context.AppUser.FindAsync(id);
        if (user != null) { _context.AppUser.Remove(user); await _context.SaveChangesAsync(); }
        return RedirectToAction("Users");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _context.AppUser.FindAsync(id);
        if (user != null) { user.IsActive = !user.IsActive; await _context.SaveChangesAsync(); }
        return RedirectToAction("Users");
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexStringLower(bytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
