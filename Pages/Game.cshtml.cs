
using GeopoliticsGame.Models;
using GeopoliticsGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeopoliticsGame.Pages;

public class GameModel : PageModel
{
    private readonly GameStateService _gameStateService;
    private readonly ILogger<GameModel> _logger;

    public GameModel(
        GameStateService gameStateService,
        ILogger<GameModel> logger)
    {
        _gameStateService = gameStateService;
        _logger = logger;
    }

    public GameSession? Game { get; private set; }

    public Country? PlayerCountry { get; private set; }

    public Player? CurrentPlayer { get; private set; }

    public bool HasLost => Game?.HasLostAllCountries() == true;

    public bool IsGameCompleted => Game?.Status == "Completed" || HasLost;

    public bool HasPendingBattle =>
        !string.IsNullOrWhiteSpace(
            Game?.PendingBattleAttackingCountryId) &&
        !string.IsNullOrWhiteSpace(
            Game?.PendingBattleDefendingCountryId);

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound(
                "Game not found. Please start a new game.");
        }

        try
        {
            Game = await _gameStateService.GetGameAsync(id);

            if (Game == null)
            {
                return NotFound(
                    "Game not found. Please start a new game.");
            }

            PlayerCountry = Game.Countries.FirstOrDefault(
                country => country.IsPlayerCountry);

            var orderedPlayers = Game.Players
                .OrderBy(player => player.TurnOrder)
                .ToList();

            if (orderedPlayers.Count == 0 ||
                Game.CurrentPlayerIndex < 0 ||
                Game.CurrentPlayerIndex >= orderedPlayers.Count)
            {
                return StatusCode(
                    500,
                    "The saved turn order is invalid.");
            }

            CurrentPlayer =
                orderedPlayers[Game.CurrentPlayerIndex];

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to load game {GameId}.",
                id);

            return StatusCode(
                503,
                "Unable to load the game from the database. Please try again.");
        }
    }

    public async Task<IActionResult> OnPostTransferSoldiersAsync(
        string id,
        string sourceCountryId,
        string destinationCountryId,
        int soldiers)
    {
        try
        {
            TempData["GameMessage"] = await _gameStateService.TransferSoldiersAsync(
                id,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                sourceCountryId,
                destinationCountryId,
                soldiers);
        }
        catch (InvalidOperationException ex)
        {
            TempData["GameMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Soldier transfer failed for game {GameId}.", id);
            TempData["GameMessage"] = "The transfer could not be completed. Please try again.";
        }

        return RedirectToPage("/Game", new { id });
    }

    public async Task<IActionResult> OnPostAttackAsync(
        string id,
        string attackingCountryId,
        string targetCountryId)
    {
        try
        {
            var result = await _gameStateService.AttackAsync(
                id,
                attackingCountryId,
                targetCountryId);

            TempData["GameMessage"] = result.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["GameMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Attack failed for game {GameId}.",
                id);

            TempData["GameMessage"] =
                "The attack could not be completed. Please try again.";
        }

        return RedirectToPage("/Game", new { id });
    }

    public async Task<IActionResult> OnPostContinueAttackAsync(
        string id)
    {
        try
        {
            var result =
                await _gameStateService.ContinueAttackAsync(id);

            TempData["GameMessage"] = result.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["GameMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to continue attack in game {GameId}.",
                id);

            TempData["GameMessage"] =
                "The next battle round could not be completed. Please try again.";
        }

        return RedirectToPage("/Game", new { id });
    }

    public async Task<IActionResult> OnPostStopAttackAsync(
        string id)
    {
        try
        {
            var result =
                await _gameStateService.StopAttackAsync(id);

            TempData["GameMessage"] = result.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["GameMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to stop attack in game {GameId}.",
                id);

            TempData["GameMessage"] =
                "The attack could not be stopped. Please try again.";
        }

        return RedirectToPage("/Game", new { id });
    }

    public async Task<IActionResult> OnPostSkipTurnAsync(string id)
    {
        try
        {
            var result =
                await _gameStateService.SkipTurnAsync(id);

            TempData["GameMessage"] = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to skip turn in game {GameId}.",
                id);

            TempData["GameMessage"] = ex.Message;
        }

        return RedirectToPage("/Game", new { id });
    }

    public async Task<IActionResult> OnPostProcessNpcTurnAsync(
        string id)
    {
        try
        {
            var result =
                await _gameStateService.ProcessNpcTurnAsync(id);

            TempData["GameMessage"] = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to process NPC turn in game {GameId}.",
                id);

            TempData["GameMessage"] = ex.Message;
        }

        return RedirectToPage("/Game", new { id });
    }

    public async Task<IActionResult> OnPostCompleteTransferAsync(
        string gameId,
        int soldiersToTransfer)
    {
        try
        {
            var result =
                await _gameStateService.CompleteTransferAsync(
                    gameId,
                    soldiersToTransfer);

            TempData["GameMessage"] = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Soldier transfer failed for game {GameId}.",
                gameId);

            TempData["GameMessage"] = ex.Message;
        }

        return RedirectToPage("/Game", new { id = gameId });
    }

    public async Task<IActionResult> OnPostAllocateReinforcementsAsync(
        string id,
        Dictionary<string, int> allocations)
    {
        try
        {
            var result =
                await _gameStateService.AllocateReinforcementsAsync(
                    id,
                    allocations ?? new Dictionary<string, int>());

            TempData["GameMessage"] = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Reinforcement allocation failed for game {GameId}.",
                id);

            TempData["GameMessage"] = ex.Message;
        }

        return RedirectToPage("/Game", new { id });
    }
}
