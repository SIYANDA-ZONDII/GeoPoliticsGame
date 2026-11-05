
using System.ComponentModel.DataAnnotations;
using GeopoliticsGame.Models;
using GeopoliticsGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GeopoliticsGame.Pages;

public class GameSetupModel : PageModel
{
    private readonly GameInitializationService _gameService;
    private readonly MongoDbConnectionService _mongoService;
    private readonly ILogger<GameSetupModel> _logger;

    public GameSetupModel(
        GameInitializationService gameService,
        MongoDbConnectionService mongoService,
        ILogger<GameSetupModel> logger)
    {
        _gameService = gameService;
        _mongoService = mongoService;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Please select your starting country.")]
    public string SelectedCountry { get; set; } = string.Empty;

    public List<SelectListItem> CountryOptions { get; private set; } = new();

    public string Message { get; private set; } = string.Empty;

    public void OnGet()
    {
        LoadCountries();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadCountries();

        if (!ModelState.IsValid)
            return Page();

        try
        {
            // Temporary placeholder until authentication is implemented.
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Account", new { register = true });
            }
            string temporaryUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            GameSession game = _gameService.CreateNewGame(
                SelectedCountry,
                temporaryUserId);

            await _mongoService.SaveGameAsync(game);

            return RedirectToPage("/Game", new { id = game.Id });
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(
                nameof(SelectedCountry),
                "Please select a valid starting country.");

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create or save a game session.");

            ModelState.AddModelError(
                string.Empty,
                "The game could not be saved. Check your database connection and try again.");

            return Page();
        }
    }

    private void LoadCountries()
    {
        CountryOptions = CountryCatalog.GetAllCountries()
            .Select(country => new SelectListItem
            {
                Value = country.Name,
                Text = country.Name
            })
            .ToList();
    }
}
