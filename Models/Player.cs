
namespace GeopoliticsGame.Models;

public class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public bool IsHuman { get; set; }

    public bool IsEliminated { get; set; }

    public List<string> ControlledCountryIds { get; set; } = new();

    public int TurnOrder { get; set; }
}
