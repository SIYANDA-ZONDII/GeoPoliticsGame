
using GeopoliticsGame.Models;

namespace GeopoliticsGame.Services;

public class GameStateService
{
    private readonly MongoDbConnectionService _mongoService;
    private readonly NpcDecisionService _npcDecisionService;
    private readonly BattleService _battleService;
    private readonly AICommentaryService _aiCommentaryService;

    public GameStateService(
        MongoDbConnectionService mongoService,
        NpcDecisionService npcDecisionService,
        BattleService battleService,
        AICommentaryService aiCommentaryService)
    {
        _mongoService = mongoService;
        _npcDecisionService = npcDecisionService;
        _battleService = battleService;
        _aiCommentaryService = aiCommentaryService;
    }

    public async Task<GameSession?> GetGameAsync(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return null;

        return await _mongoService.GetGameAsync(gameId);
    }

    public async Task<bool> SaveGameAsync(GameSession game)
    {
        if (game == null || string.IsNullOrWhiteSpace(game.Id))
            return false;

        return await _mongoService.UpdateGameAsync(game);
    }

    public async Task<(GameSession? Game, string Message)> SkipTurnAsync(
        string gameId)
    {
        var game = await GetGameAsync(gameId);

        if (game == null)
            return (null, "Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);
        EnsureNoPendingTransfer(game);
        EnsureNoPendingAllocation(game);
        EnsureNoPendingBattle(game);

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!currentPlayer.IsHuman)
            throw new InvalidOperationException(
                "It is not the human player's turn.");

        string message = $"{currentPlayer.Name} skipped their turn.";
        message += Environment.NewLine + AdvanceTurn(game);

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException(
                "The game could not be saved.");

        return (game, message);
    }

    public async Task<(GameSession Game, string Message)> ProcessNpcTurnAsync(
        string gameId)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);
        EnsureNoPendingTransfer(game);
        EnsureNoPendingAllocation(game);
        EnsureNoPendingBattle(game);

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (currentPlayer.IsHuman)
            throw new InvalidOperationException(
                "It is the human player's turn, not an NPC's turn.");

        var decision = _npcDecisionService.EvaluateTurn(
            game,
            currentPlayer);

        string message;

        if (!decision.ShouldAttack)
        {
            message = decision.Rationale;
        }
        else
        {
            var attacker = game.Countries.FirstOrDefault(
                c => c.Id == decision.AttackingCountryId);

            var target = game.Countries.FirstOrDefault(
                c => c.Id == decision.TargetCountryId);

            if (attacker == null ||
                target == null ||
                attacker.OwnerId != currentPlayer.Id ||
                attacker.Soldiers <= 1 ||
                target.OwnerId == null ||
                target.OwnerId == currentPlayer.Id ||
                target.Soldiers <= 0)
            {
                message =
                    $"{currentPlayer.Name} could not execute its planned attack.";
            }
            else
            {
                var battleResult = _battleService.ResolveBattle(
                    game,
                    currentPlayer,
                    attacker,
                    target);

                message = string.Join(
                    Environment.NewLine,
                    battleResult.BattleLog);

                string commentary =
                    await _aiCommentaryService.GenerateCommentaryAsync(
                        attacker.Name,
                        target.Name,
                        string.Join(" ", battleResult.BattleLog));

                message += Environment.NewLine
                    + "AI Commentary: " + commentary;

                if (battleResult.WasConquered)
                {
                    int reward = Math.Max(
                        2,
                        battleResult.DefenderInitialSoldiers / 2);

                    // ResolveBattle assigns ownership and removes
                    // the defeated country from its previous owner.
                    attacker.Soldiers += reward;

                    int transfer = Math.Min(
                        reward,
                        Math.Max(0, attacker.Soldiers - 1));

                    attacker.Soldiers -= transfer;
                    target.Soldiers = transfer;

                    message += Environment.NewLine
                        + $"{currentPlayer.Name} received {reward} bonus soldiers "
                        + $"and transferred {transfer} soldiers to {target.Name}.";

                    if (game.HasLostAllCountries())
                    {
                        game.Status = "Completed";
                        message += Environment.NewLine
                            + "You lost your last country.";
                    }
                    else if (game.Countries.All(
                        c => c.OwnerId == currentPlayer.Id))
                    {
                        game.Status = "Completed";

                        message += Environment.NewLine
                            + $"{currentPlayer.Name} has conquered all countries and won the game!";
                    }
                }
            }
        }

        if (game.Status != "Completed")
            message += Environment.NewLine + AdvanceTurn(game);

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException(
                "The game could not be saved after the NPC turn.");

        return (game, message);
    }

    public async Task<string> TransferSoldiersAsync(
        string gameId,
        string? userId,
        string sourceCountryId,
        string destinationCountryId,
        int soldiers)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        if (string.IsNullOrEmpty(userId) || game.OwnerUserId != userId)
            throw new InvalidOperationException("Only the campaign owner can transfer soldiers.");

        ValidateGame(game);
        EnsureGameInProgress(game);
        EnsureNoPendingBattle(game);
        EnsureNoPendingTransfer(game);
        EnsureNoPendingAllocation(game);

        var player = game.Players.OrderBy(p => p.TurnOrder).ElementAt(game.CurrentPlayerIndex);
        if (!player.IsHuman)
            throw new InvalidOperationException("You can transfer soldiers only on your turn.");

        var source = game.Countries.FirstOrDefault(c => c.Id == sourceCountryId);
        var destination = game.Countries.FirstOrDefault(c => c.Id == destinationCountryId);

        if (source is null || destination is null || source.Id == destination.Id ||
            source.OwnerId != player.Id || destination.OwnerId != player.Id)
        {
            throw new InvalidOperationException("Choose two different countries you control.");
        }

        if (soldiers < 1 || soldiers >= source.Soldiers)
            throw new InvalidOperationException("Transfer at least one soldier and leave at least one behind.");

        source.Soldiers -= soldiers;
        destination.Soldiers += soldiers;

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException("The game changed. Review the current board and try again.");

        return $"Transferred {soldiers} soldiers from {source.Name} to {destination.Name}.";
    }

    // Start a human attack and resolve exactly one combat round.
    public async Task<(GameSession Game, string Message)> AttackAsync(
        string gameId,
        string attackingCountryId,
        string targetCountryId)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);
        EnsureNoPendingTransfer(game);
        EnsureNoPendingAllocation(game);
        EnsureNoPendingBattle(game);

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!currentPlayer.IsHuman)
            throw new InvalidOperationException(
                "You cannot attack during an NPC's turn.");

        var attacker = game.Countries.FirstOrDefault(
            c => c.Id == attackingCountryId);

        var target = game.Countries.FirstOrDefault(
            c => c.Id == targetCountryId);

        if (attacker == null || target == null)
            throw new InvalidOperationException(
                "The attacking or defending country could not be found.");

        if (attacker.Id == target.Id)
            throw new InvalidOperationException(
                "A country cannot attack itself.");

        if (attacker.OwnerId != currentPlayer.Id ||
            attacker.Soldiers <= 1)
            throw new InvalidOperationException(
                "Choose a country you own with at least two soldiers.");

        if (target.OwnerId == null ||
            target.OwnerId == currentPlayer.Id ||
            target.Soldiers <= 0)
            throw new InvalidOperationException(
                "Choose a living country owned by an opponent.");

        game.PendingBattleAttackingCountryId = attacker.Id;
        game.PendingBattleDefendingCountryId = target.Id;
        game.PendingBattlePlayerId = currentPlayer.Id;
        game.PendingBattleInitialAttackerSoldiers = attacker.Soldiers;
        game.PendingBattleInitialDefenderSoldiers = target.Soldiers;
        game.PendingBattleAttackerLosses = 0;
        game.PendingBattleDefenderLosses = 0;
        game.PendingBattleLog = new List<string>
        {
            $"{currentPlayer.Name} attacks {target.Name} from {attacker.Name}."
        };

        return await ProcessHumanBattleRoundAsync(game);
    }

    // Resolve one additional round of an existing attack.
    public async Task<(GameSession Game, string Message)> ContinueAttackAsync(
        string gameId)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);

        if (string.IsNullOrWhiteSpace(game.PendingBattlePlayerId))
            throw new InvalidOperationException(
                "There is no ongoing attack.");

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!currentPlayer.IsHuman ||
            currentPlayer.Id != game.PendingBattlePlayerId)
            throw new InvalidOperationException(
                "This attack does not belong to the current player.");

        return await ProcessHumanBattleRoundAsync(game);
    }

    // Stop the attack, preserve remaining soldiers and end the turn.
    public async Task<(GameSession Game, string Message)> StopAttackAsync(
        string gameId)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);

        if (string.IsNullOrWhiteSpace(game.PendingBattlePlayerId))
            throw new InvalidOperationException(
                "There is no ongoing attack.");

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!currentPlayer.IsHuman ||
            currentPlayer.Id != game.PendingBattlePlayerId)
            throw new InvalidOperationException(
                "This attack does not belong to the current player.");

        var attacker = game.Countries.FirstOrDefault(
            c => c.Id == game.PendingBattleAttackingCountryId)
            ?? throw new InvalidOperationException(
                "Attacking country not found.");

        var target = game.Countries.FirstOrDefault(
            c => c.Id == game.PendingBattleDefendingCountryId)
            ?? throw new InvalidOperationException(
                "Defending country not found.");

        string message =
            $"You stopped attacking {target.Name}. " +
            $"{attacker.Name} retains {attacker.Soldiers} soldiers; " +
            $"{target.Name} retains {target.Soldiers} soldiers.";

        ClearPendingBattle(game);
        message += Environment.NewLine + AdvanceTurn(game);

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException(
                "The game could not be saved after stopping the attack.");

        return (game, message);
    }

    private async Task<(GameSession Game, string Message)>
        ProcessHumanBattleRoundAsync(GameSession game)
    {
        var attacker = game.Countries.FirstOrDefault(
            c => c.Id == game.PendingBattleAttackingCountryId)
            ?? throw new InvalidOperationException(
                "Attacking country not found.");

        var target = game.Countries.FirstOrDefault(
            c => c.Id == game.PendingBattleDefendingCountryId)
            ?? throw new InvalidOperationException(
                "Defending country not found.");

        var currentPlayer = game.Players
            .OrderBy(p => p.TurnOrder)
            .ElementAt(game.CurrentPlayerIndex);

        if (attacker.OwnerId != currentPlayer.Id ||
            target.OwnerId == currentPlayer.Id ||
            target.Soldiers <= 0)
            throw new InvalidOperationException(
                "The saved battle state is no longer valid.");

        if (attacker.Soldiers <= 1)
            throw new InvalidOperationException(
                "Your attacking country has only one soldier.");

        var round = _battleService.ResolveBattleRound(
            attacker,
            target);

        game.PendingBattleAttackerLosses += round.AttackerLosses;
        game.PendingBattleDefenderLosses += round.DefenderLosses;
        game.PendingBattleLog.AddRange(round.Logs);

        string message = string.Join(
            Environment.NewLine,
            round.Logs);

        string commentary =
            await _aiCommentaryService.GenerateCommentaryAsync(
                attacker.Name,
                target.Name,
                string.Join(" ", round.Logs));

        message += Environment.NewLine
            + "AI Commentary: " + commentary;

        bool conquered = target.Soldiers == 0;
        bool attackerExhausted = attacker.Soldiers <= 1;

        if (conquered)
        {
            var previousOwner = game.Players.FirstOrDefault(
                p => p.Id == target.OwnerId);

            int reward = Math.Max(
                2,
                game.PendingBattleInitialDefenderSoldiers / 2);

            target.OwnerId = currentPlayer.Id;
            target.Soldiers = 0;

            if (!currentPlayer.ControlledCountryIds.Contains(target.Id))
                currentPlayer.ControlledCountryIds.Add(target.Id);

            previousOwner?.ControlledCountryIds.Remove(target.Id);

            // Add the reward to the attacking army.
            attacker.Soldiers += reward;

            game.PendingTransferFromCountryId = attacker.Id;
            game.PendingTransferToCountryId = target.Id;
            game.PendingTransferPlayerId = currentPlayer.Id;
            game.PendingTransferReward = reward;
            game.PendingReinforcements = reward;

            message += Environment.NewLine
                + $"Victory! You conquered {target.Name}. "
                + $"Your conquest reward is {reward} soldiers. "
                + "Choose how many to transfer.";

            ClearPendingBattle(game);
        }
        else if (attackerExhausted)
        {
            message += Environment.NewLine
                + "Your attack has ended because only one soldier remains. "
                + "Your country is preserved.";

            ClearPendingBattle(game);
            message += Environment.NewLine + AdvanceTurn(game);
        }
        else
        {
            message += Environment.NewLine
                + "Battle paused after this round. "
                + "Choose Continue Attack or Stop Attack.";
        }

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException(
                "The battle round could not be saved.");

        return (game, message);
    }

    private static void ClearPendingBattle(GameSession game)
    {
        game.PendingBattleAttackingCountryId = null;
        game.PendingBattleDefendingCountryId = null;
        game.PendingBattlePlayerId = null;
        game.PendingBattleInitialAttackerSoldiers = 0;
        game.PendingBattleInitialDefenderSoldiers = 0;
        game.PendingBattleAttackerLosses = 0;
        game.PendingBattleDefenderLosses = 0;
        game.PendingBattleLog = new List<string>();
    }

    public async Task<(GameSession Game, string Message)> CompleteTransferAsync(
        string gameId,
        int soldiersToTransfer)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);
        EnsureNoPendingBattle(game);

        if (string.IsNullOrWhiteSpace(game.PendingTransferPlayerId) ||
            string.IsNullOrWhiteSpace(game.PendingTransferFromCountryId) ||
            string.IsNullOrWhiteSpace(game.PendingTransferToCountryId))
        {
            throw new InvalidOperationException(
                "There is no pending conquest transfer.");
        }

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!currentPlayer.IsHuman ||
            currentPlayer.Id != game.PendingTransferPlayerId)
        {
            throw new InvalidOperationException(
                "The pending transfer does not belong to the current human player.");
        }

        var attacker = game.Countries.FirstOrDefault(
            c => c.Id == game.PendingTransferFromCountryId);

        var target = game.Countries.FirstOrDefault(
            c => c.Id == game.PendingTransferToCountryId);

        if (attacker == null || target == null)
            throw new InvalidOperationException(
                "The countries involved in the transfer could not be found.");

        if (attacker.OwnerId != currentPlayer.Id ||
            target.OwnerId != currentPlayer.Id)
        {
            throw new InvalidOperationException(
                "Both countries must be owned by the current player.");
        }

        int maxTransfer = attacker.Soldiers - 1;

        if (soldiersToTransfer < 1 || soldiersToTransfer > maxTransfer)
        {
            throw new InvalidOperationException(
                $"Transfer between 1 and {maxTransfer} soldiers. " +
                "Leave at least one soldier in the attacking country.");
        }

        if (attacker.Soldiers - soldiersToTransfer < 1)
            throw new InvalidOperationException(
                "The attacking country must retain at least one soldier.");

        attacker.Soldiers -= soldiersToTransfer;
        target.Soldiers += soldiersToTransfer;

        game.PendingTransferFromCountryId = null;
        game.PendingTransferToCountryId = null;
        game.PendingTransferPlayerId = null;
        game.PendingTransferReward = 0;
        game.PendingReinforcements = 0;

        string message =
            $"Transferred {soldiersToTransfer} soldiers to {target.Name}.";

        if (game.Countries.All(c => c.OwnerId == currentPlayer.Id))
        {
            game.Status = "Completed";
            game.PendingAllocationCount = 0;
            game.PendingAllocationPlayerId = null;

            message += Environment.NewLine
                + "Congratulations! You have conquered all 10 countries and won the game.";
        }
        else
        {
            message += Environment.NewLine + AdvanceTurn(game);
        }

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException(
                "The game could not be saved after the transfer.");

        return (game, message);
    }

    public async Task<(GameSession Game, string Message)> AllocateReinforcementsAsync(
        string gameId,
        Dictionary<string, int> allocations)
    {
        var game = await GetGameAsync(gameId)
            ?? throw new InvalidOperationException("Game not found.");

        ValidateGame(game);
        EnsureGameInProgress(game);
        EnsureNoPendingTransfer(game);
        EnsureNoPendingBattle(game);

        if (game.PendingAllocationCount <= 0 ||
            string.IsNullOrWhiteSpace(game.PendingAllocationPlayerId))
        {
            throw new InvalidOperationException(
                "There are no reinforcements waiting to be allocated.");
        }

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        var currentPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!currentPlayer.IsHuman ||
            currentPlayer.Id != game.PendingAllocationPlayerId)
        {
            throw new InvalidOperationException(
                "These reinforcements do not belong to the current human player.");
        }

        allocations ??= new Dictionary<string, int>();

        var ownedCountries = game.Countries
            .Where(c => c.OwnerId == currentPlayer.Id)
            .ToList();

        foreach (var entry in allocations)
        {
            if (entry.Value < 0)
                throw new InvalidOperationException(
                    "Soldier allocations cannot be negative.");

            if (!ownedCountries.Any(c => c.Id == entry.Key))
                throw new InvalidOperationException(
                    "You can allocate reinforcements only to countries you own.");
        }

        int totalAllocated = allocations.Values.Sum();

        if (totalAllocated != game.PendingAllocationCount)
            throw new InvalidOperationException(
                $"You must allocate exactly {game.PendingAllocationCount} soldiers. "
                + $"You have allocated {totalAllocated}.");

        foreach (var country in ownedCountries)
        {
            if (allocations.TryGetValue(country.Id, out int amount))
                country.Soldiers += amount;
        }

        int allocatedCount = game.PendingAllocationCount;

        game.PendingAllocationCount = 0;
        game.PendingAllocationPlayerId = null;

        string message =
            $"Successfully allocated {allocatedCount} reinforcement soldiers.";

        if (!await _mongoService.UpdateGameAsync(game))
            throw new InvalidOperationException(
                "The game could not be saved after reinforcement allocation.");

        return (game, message);
    }

    private string AdvanceTurn(GameSession game)
    {
        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        if (orderedPlayers.Count == 0)
            throw new InvalidOperationException(
                "The game has no players.");

        // Skip eliminated players.
        int attempts = 0;

        do
        {
            game.CurrentPlayerIndex++;

            if (game.CurrentPlayerIndex >= orderedPlayers.Count)
            {
                game.CurrentPlayerIndex = 0;
                game.RoundNumber++;
            }

            attempts++;

            var candidate = orderedPlayers[game.CurrentPlayerIndex];

            bool stillOwnsCountries = game.Countries.Any(
                c => c.OwnerId == candidate.Id);

            if (stillOwnsCountries)
                break;

        } while (attempts <= orderedPlayers.Count);

        var nextPlayer = orderedPlayers[game.CurrentPlayerIndex];

        if (!game.Countries.Any(c => c.OwnerId == nextPlayer.Id))
        {
            game.Status = "Completed";
            return "The game has ended because no eligible players remain.";
        }

        if (game.RoundNumber >= 2)
        {
            var ownedCountries = game.Countries
                .Where(c => c.OwnerId == nextPlayer.Id)
                .ToList();

            int reinforcementCount = ownedCountries.Count * 2;

            if (reinforcementCount > 0)
            {
                if (nextPlayer.IsHuman)
                {
                    game.PendingAllocationPlayerId = nextPlayer.Id;
                    game.PendingAllocationCount = reinforcementCount;

                    return $"{nextPlayer.Name}, allocate your "
                        + $"{reinforcementCount} reinforcement soldiers "
                        + $"among your {ownedCountries.Count} countries.";
                }

                var weakestCountry = ownedCountries
                    .OrderBy(c => c.Soldiers)
                    .First();

                weakestCountry.Soldiers += reinforcementCount;

                return $"{nextPlayer.Name} received {reinforcementCount} "
                    + $"reinforcements and placed them in {weakestCountry.Name}.";
            }
        }

        return $"It is now {nextPlayer.Name}'s turn.";
    }

    private static void ValidateGame(GameSession game)
    {
        if (game.Players == null || game.Players.Count == 0)
            throw new InvalidOperationException(
                "The game has no players.");

        var orderedPlayers = game.Players
            .OrderBy(p => p.TurnOrder)
            .ToList();

        if (game.CurrentPlayerIndex < 0 ||
            game.CurrentPlayerIndex >= orderedPlayers.Count)
        {
            throw new InvalidOperationException(
                "The current player index is invalid.");
        }

        if (game.Countries == null || game.Countries.Count == 0)
            throw new InvalidOperationException(
                "The game has no countries.");
    }

    private static void EnsureGameInProgress(GameSession game)
    {
        if (game.Status == "Completed" || game.HasLostAllCountries())
            throw new InvalidOperationException(
                "The game has already ended.");
    }

    private static void EnsureNoPendingTransfer(GameSession game)
    {
        if (!string.IsNullOrWhiteSpace(game.PendingTransferPlayerId))
            throw new InvalidOperationException(
                "Complete the conquest soldier transfer before taking another action.");
    }

    private static void EnsureNoPendingAllocation(GameSession game)
    {
        if (game.PendingAllocationCount > 0)
            throw new InvalidOperationException(
                "Allocate your reinforcement soldiers before taking another action.");
    }

    private static void EnsureNoPendingBattle(GameSession game)
    {
        if (!string.IsNullOrWhiteSpace(game.PendingBattlePlayerId))
            throw new InvalidOperationException(
                "Continue or stop the ongoing attack before taking another action.");
    }
}
