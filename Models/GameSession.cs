
namespace GeopoliticsGame.Models;

public class GameSession
{
    public string Id { get; set; } =
        Guid.NewGuid().ToString();

    public string? Revision { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public List<Country> Countries { get; set; } = new();

    public List<Player> Players { get; set; } = new();

    public int CurrentPlayerIndex { get; set; }

    public int RoundNumber { get; set; } = 1;

    public string Status { get; set; } = "Setup";

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    // Conquest transfer information
    public int PendingReinforcements { get; set; } = 0;

    public string? PendingTransferFromCountryId { get; set; }

    public string? PendingTransferToCountryId { get; set; }

    public string? PendingTransferPlayerId { get; set; }

    public int PendingTransferReward { get; set; } = 0;

    // Manual reinforcement allocation information
    public string? PendingAllocationPlayerId { get; set; }

    public int PendingAllocationCount { get; set; } = 0;

    // Interactive human battle state
    public string? PendingBattleAttackingCountryId { get; set; }

    public string? PendingBattleDefendingCountryId { get; set; }

    public string? PendingBattlePlayerId { get; set; }

    public int PendingBattleInitialAttackerSoldiers { get; set; }

    public int PendingBattleInitialDefenderSoldiers { get; set; }

    public int PendingBattleAttackerLosses { get; set; }

    public int PendingBattleDefenderLosses { get; set; }

    public List<string> PendingBattleLog { get; set; } = new();

    public bool HasLostAllCountries()
    {
        var human = Players.FirstOrDefault(player => player.IsHuman);
        return human is not null && Countries.All(country => country.OwnerId != human.Id);
    }
}
