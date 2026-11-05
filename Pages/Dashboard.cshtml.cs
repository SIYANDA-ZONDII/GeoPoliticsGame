using System.Security.Claims;
using GeopoliticsGame.Models;
using GeopoliticsGame.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeopoliticsGame.Pages;

[Authorize]
public class DashboardModel(MongoDbConnectionService database) : PageModel
{
    public List<GameSession> Games { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Forbid();

        Games = await database.GetGamesForUserAsync(userId);
        return Page();
    }
}