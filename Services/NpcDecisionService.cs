using GeopoliticsGame.Models;

namespace GeopoliticsGame.Services;

public class NpcDecisionService
{
    public NpcDecision EvaluateTurn(GameSession game, Player npc)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));

        if (npc is null)
            throw new ArgumentNullException(nameof(npc));

        if (npc.IsHuman)
            throw new InvalidOperationException(
                "This service evaluates NPC turns only.");

        // Find the NPC's countries that have soldiers available to attack.
        var attackingCountries = game.Countries
            .Where(country =>
                country.OwnerId == npc.Id &&
                country.Soldiers > 1)
            .OrderByDescending(country => country.Soldiers)
            .ToList();

        if (attackingCountries.Count == 0)
        {
            return NpcDecision.Skip(
                $"{npc.Name} has no country with enough soldiers to attack.");
        }

        // Consider countries controlled by other players.
        var enemyCountries = game.Countries
            .Where(country =>
                country.OwnerId != null &&
                country.OwnerId != npc.Id)
            .ToList();

        if (enemyCountries.Count == 0)
        {
            return NpcDecision.Skip(
                $"{npc.Name} has no enemy territories to attack.");
        }

        // Evaluate attacks using available soldiers versus defenders.
        var possibleAttacks = (
            from attacker in attackingCountries
            from target in enemyCountries
            let availableSoldiers = attacker.Soldiers - 1
            let strengthRatio =
                (double)availableSoldiers /
                Math.Max(1, target.Soldiers)
            where availableSoldiers > target.Soldiers
            orderby strengthRatio descending,
                    target.Soldiers ascending
            select new
            {
                Attacker = attacker,
                Target = target,
                AvailableSoldiers = availableSoldiers,
                StrengthRatio = strengthRatio
            })
            .ToList();

        if (possibleAttacks.Count == 0)
        {
            return NpcDecision.Skip(
                $"{npc.Name} skips because no available attack has a " +
                "numerical advantage while leaving one soldier behind.");
        }

        var bestAttack = possibleAttacks[0];

        return NpcDecision.Attack(
            bestAttack.Attacker.Id,
            bestAttack.Target.Id,
            $"{npc.Name} chooses to attack {bestAttack.Target.Name} " +
            $"from {bestAttack.Attacker.Name}. It can commit " +
            $"{bestAttack.AvailableSoldiers} soldiers while leaving " +
            $"one behind, against {bestAttack.Target.Soldiers} defenders.");
    }
}

public class NpcDecision
{
    public bool ShouldAttack { get; private set; }

    public string? AttackingCountryId { get; private set; }

    public string? TargetCountryId { get; private set; }

    public string Rationale { get; private set; } = string.Empty;

    private NpcDecision()
    {
    }

    public static NpcDecision Attack(
        string attackingCountryId,
        string targetCountryId,
        string rationale)
    {
        return new NpcDecision
        {
            ShouldAttack = true,
            AttackingCountryId = attackingCountryId,
            TargetCountryId = targetCountryId,
            Rationale = rationale
        };
    }

    public static NpcDecision Skip(string rationale)
    {
        return new NpcDecision
        {
            ShouldAttack = false,
            Rationale = rationale
        };
    }
}