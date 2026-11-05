
using GeopoliticsGame.Models;
using GeoPoliticsGame.Services;

namespace GeopoliticsGame.Services;

public class GameInitializationService
{
    public GameSession CreateNewGame(
        string selectedCountry,
        string ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(selectedCountry))
        {
            throw new ArgumentException(
                "A starting country must be selected.",
                nameof(selectedCountry));
        }

        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "A game owner must be provided.",
                nameof(ownerUserId));
        }

        var allCountries = CountryCatalog.GetAllCountries();

        var startingCountry = allCountries.FirstOrDefault(
            country => country.Name.Equals(
                selectedCountry,
                StringComparison.OrdinalIgnoreCase));

        if (startingCountry == null)
        {
            throw new ArgumentException(
                "The selected country is not in the country catalogue.",
                nameof(selectedCountry));
        }

        // Keep the selected country and randomly choose nine others.
        var selectedCountries = new List<Country>
        {
            startingCountry
        };

        var otherCountries = allCountries
            .Where(country => country.Name != startingCountry.Name)
            .OrderBy(_ => Random.Shared.Next())
            .Take(9)
            .ToList();

        selectedCountries.AddRange(otherCountries);

        // Create one human player and nine NPCs.
        var players = new List<Player>
        {
            new()
            {
                Name = "Human Player",
                IsHuman = true
            }
        };

        for (int i = 1; i <= 9; i++)
        {
            players.Add(new Player
            {
                Name = $"Computer {i}",
                IsHuman = false
            });
        }

        // Randomize turn order.
        var shuffledPlayers = players
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        for (int i = 0; i < shuffledPlayers.Count; i++)
        {
            shuffledPlayers[i].TurnOrder = i;
        }

        // Assign one country to each player.
        for (int i = 0; i < selectedCountries.Count; i++)
        {
            var country = selectedCountries[i];
            var player = players[i];

            country.Soldiers =
                PopulationSoldierCalculator.CalculateSoldiers(
                    country.Population);

            country.OwnerId = player.Id;
            country.IsPlayerCountry = player.IsHuman;

            player.ControlledCountryIds.Add(country.Id);
        }

        return new GameSession
        {
            OwnerUserId = ownerUserId,
            Countries = selectedCountries,
            Players = shuffledPlayers,
            CurrentPlayerIndex = 0,
            RoundNumber = 1,
            Status = "InProgress"
        };
    }
}
