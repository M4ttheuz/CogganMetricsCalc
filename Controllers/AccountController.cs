using System.Security.Claims;
using inzynierka.Data;
using inzynierka.Models.Entities;
using inzynierka.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace inzynierka.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public AccountController(AppDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Activity");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLower();

        var emailExists = await _context.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "Użytkownik o takim adresie e-mail już istnieje.");
            return View(model);
        }

        var user = new User
        {
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var initialDate = DateTime.UtcNow.Date.AddYears(-1);

            var ftpEntry = new UserFtpHistory
            {
                UserId = user.Id,
                FtpValue = model.InitialFtp,
                EffectiveDate = initialDate
            };

            var weightEntry = new UserWeightHistory
            {
                UserId = user.Id,
                WeightKg = Math.Round(model.WeightKg, 1),
                EffectiveDate = initialDate
            };

            _context.UserFtpHistories.Add(ftpEntry);
            _context.UserWeightHistories.Add(weightEntry);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas rejestracji. Spróbuj ponownie.");
            return View(model);
        }

        await SignInUserAsync(user);

        return RedirectToAction("Index", "Activity");
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Activity");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLower();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Błędny e-mail lub hasło.");
            return View(model);
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Błędny e-mail lub hasło.");
            return View(model);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
            await _context.SaveChangesAsync();
        }

        await SignInUserAsync(user);

        return RedirectToAction("Index", "Activity");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }
}