
using System.Collections.Concurrent;
using GeopoliticsGame.Models;

namespace GeopoliticsGame.Services;

public class GameSessionStore
{
    private readonly ConcurrentDictionary<string, GameSession> _games = new();

    public void Save(GameSession game)
    {
        _games[game.Id] = game;
    }

    public bool TryGet(string id, out GameSession? game)
    {
        bool found = _games.TryGetValue(id, out var result);

        game = result;
        return found;
    }
}
