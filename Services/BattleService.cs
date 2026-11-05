
using GeopoliticsGame.Models;

namespace GeopoliticsGame.Services;

public class BattleService
{
    // Existing complete battle method retained for NPCs.
    public BattleResult ResolveBattle(
        GameSession game,
        Player attackerPlayer,
        Country attackingCountry,
        Country defendingCountry)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(attackerPlayer);
        ArgumentNullException.ThrowIfNull(attackingCountry);
        ArgumentNullException.ThrowIfNull(defendingCountry);

        ValidateAttack(
            attackerPlayer,
            attackingCountry,
            defendingCountry);

        int originalDefenderSoldiers = defendingCountry.Soldiers;
        int initialAttackerSoldiers = attackingCountry.Soldiers;

        var previousOwner = game.Players.FirstOrDefault(
            player => player.Id == defendingCountry.OwnerId);

        int attackerLosses = 0;
        int defenderLosses = 0;

        var battleLog = new List<string>
        {
            $"{attackerPlayer.Name} attacks {defendingCountry.Name} " +
            $"from {attackingCountry.Name}."
        };

        while (attackingCountry.Soldiers > 1 &&
               defendingCountry.Soldiers > 0)
        {
            int diceCount = Math.Min(
                attackingCountry.Soldiers - 1,
                defendingCountry.Soldiers);

            for (int i = 0; i < diceCount; i++)
            {
                int attackRoll = Random.Shared.Next(1, 7);
                int defenceRoll = Random.Shared.Next(1, 7);

                battleLog.Add(
                    $"Pair {i + 1}: attacker rolled {attackRoll}; " +
                    $"defender rolled {defenceRoll}.");

                if (attackRoll > defenceRoll)
                {
                    defendingCountry.Soldiers--;
                    defenderLosses++;

                    battleLog.Add(
                        $"{defendingCountry.Name} loses one soldier.");
                }
                else if (defenceRoll > attackRoll)
                {
                    attackingCountry.Soldiers--;
                    attackerLosses++;

                    battleLog.Add(
                        $"{attackingCountry.Name} loses one soldier.");
                }
                else
                {
                    battleLog.Add(
                        $"Tie at {attackRoll}. No soldiers are lost.");
                }

                if (attackingCountry.Soldiers <= 1 ||
                    defendingCountry.Soldiers <= 0)
                {
                    break;
                }
            }
        }

        bool conquered = defendingCountry.Soldiers == 0;
        int reward = 0;

        if (conquered)
        {
            defendingCountry.OwnerId = attackerPlayer.Id;

            reward = Math.Max(
                2,
                originalDefenderSoldiers / 2);

            if (!attackerPlayer.ControlledCountryIds.Contains(
                    defendingCountry.Id))
            {
                attackerPlayer.ControlledCountryIds.Add(
                    defendingCountry.Id);
            }

            previousOwner?.ControlledCountryIds.Remove(
                defendingCountry.Id);

            battleLog.Add(
                $"{attackerPlayer.Name} conquers " +
                $"{defendingCountry.Name}.");

            battleLog.Add(
                $"Conquest reward: {reward} soldiers.");
        }
        else
        {
            battleLog.Add(
                $"The attack ends. {defendingCountry.Name} " +
                "remains under its current owner's control.");
        }

        battleLog.Add(
            $"Battle summary: attacker lost {attackerLosses}; " +
            $"defender lost {defenderLosses}.");

        return new BattleResult
        {
            AttackingCountryName = attackingCountry.Name,
            DefendingCountryName = defendingCountry.Name,
            AttackerInitialSoldiers = initialAttackerSoldiers,
            DefenderInitialSoldiers = originalDefenderSoldiers,
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            WasConquered = conquered,
            ReinforcementReward = reward,
            BattleLog = battleLog
        };
    }

    // New method: resolve only ONE combat round.
    public BattleRoundResult ResolveBattleRound(
        Country attackingCountry,
        Country defendingCountry)
    {
        ArgumentNullException.ThrowIfNull(attackingCountry);
        ArgumentNullException.ThrowIfNull(defendingCountry);

        if (attackingCountry.Id == defendingCountry.Id)
            throw new InvalidOperationException(
                "A country cannot attack itself.");

        if (attackingCountry.Soldiers <= 1)
            throw new InvalidOperationException(
                "At least two soldiers are required to attack.");

        if (defendingCountry.Soldiers <= 0)
            throw new InvalidOperationException(
                "The defending country has already been conquered.");

        int diceCount = Math.Min(
            attackingCountry.Soldiers - 1,
            defendingCountry.Soldiers);

        int attackerLosses = 0;
        int defenderLosses = 0;

        var logs = new List<string>
        {
            $"Combat round: {attackingCountry.Name} vs. " +
            $"{defendingCountry.Name}."
        };

        for (int i = 0; i < diceCount; i++)
        {
            int attackRoll = Random.Shared.Next(1, 7);
            int defenceRoll = Random.Shared.Next(1, 7);

            logs.Add(
                $"Pair {i + 1}: attacker rolled {attackRoll}; " +
                $"defender rolled {defenceRoll}.");

            if (attackRoll > defenceRoll)
            {
                defendingCountry.Soldiers--;
                defenderLosses++;

                logs.Add(
                    $"{defendingCountry.Name} loses one soldier.");
            }
            else if (defenceRoll > attackRoll)
            {
                attackingCountry.Soldiers--;
                attackerLosses++;

                logs.Add(
                    $"{attackingCountry.Name} loses one soldier.");
            }
            else
            {
                logs.Add(
                    $"Tie at {attackRoll}. No soldiers are lost.");
            }

            if (attackingCountry.Soldiers <= 1 ||
                defendingCountry.Soldiers <= 0)
            {
                break;
            }
        }

        logs.Add(
            $"After this round: {attackingCountry.Name} has " +
            $"{attackingCountry.Soldiers}; {defendingCountry.Name} has " +
            $"{defendingCountry.Soldiers} soldiers.");

        return new BattleRoundResult
        {
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            Logs = logs
        };
    }

    private static void ValidateAttack(
        Player attackerPlayer,
        Country attackingCountry,
        Country defendingCountry)
    {
        if (attackingCountry.Id == defendingCountry.Id)
            throw new InvalidOperationException(
                "A country cannot attack itself.");

        if (attackingCountry.OwnerId != attackerPlayer.Id)
            throw new InvalidOperationException(
                "You do not own the attacking country.");

        if (attackingCountry.Soldiers <= 1)
            throw new InvalidOperationException(
                "At least two soldiers are required to attack.");

        if (defendingCountry.OwnerId == attackerPlayer.Id ||
            defendingCountry.OwnerId == null ||
            defendingCountry.Soldiers <= 0)
            throw new InvalidOperationException(
                "The selected country cannot be attacked.");
    }
}

public class BattleRoundResult
{
    public int AttackerLosses { get; set; }

    public int DefenderLosses { get; set; }

    public List<string> Logs { get; set; } = new();
}

public class BattleResult
{
    public string AttackingCountryName { get; set; } = string.Empty;

    public string DefendingCountryName { get; set; } = string.Empty;

    public int AttackerInitialSoldiers { get; set; }

    public int DefenderInitialSoldiers { get; set; }

    public int AttackerLosses { get; set; }

    public int DefenderLosses { get; set; }

    public bool WasConquered { get; set; }

    public int ReinforcementReward { get; set; }

    public List<string> BattleLog { get; set; } = new();
}
