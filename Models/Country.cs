
namespace GeopoliticsGame.Models;

public class Country
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public long Population { get; set; }

    public int Soldiers { get; set; }

    public string? OwnerId { get; set; }

    public bool IsPlayerCountry { get; set; }
}
