using GeopoliticsGame.Models;
using GeopoliticsGame.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace GeopoliticsGame.Pages;

public class AccountModel(
    AccountService accounts,
    ILogger<AccountModel> logger) : PageModel
{
    [BindProperty]
    [Required]
    [StringLength(40, MinimumLength = 3)]
    [RegularExpression(@"[a-zA-Z0-9_.-]+",
        ErrorMessage = "Use only letters, numbers, dots, underscores or hyphens.")]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public bool Register { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        AppUser? user;

        try
        {
            user = Register
                ? await accounts.RegisterAsync(Username, Password)
                : await accounts.LoginAsync(Username, Password);
        }
        catch (InvalidOperationException ex)
            when (ex.InnerException is MongoWriteException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogError(ex, "Account operation failed.");
            ModelState.AddModelError(
                string.Empty, "The account service is unavailable. Please try again.");

            return Page();
        }

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var identity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToPage("/Index");
    }
}