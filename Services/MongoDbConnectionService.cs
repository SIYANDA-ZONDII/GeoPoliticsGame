
using GeopoliticsGame.Models;
using GeopoliticsGame.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GeopoliticsGame.Services;

public class MongoDbConnectionService
{
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;

    public MongoDbConnectionService(
        IMongoClient client,
        IOptions<MongoDbSettings> options)
    {
        _client = client;
        _settings = options.Value;
    }
    public async Task TestConnectionAsync()
    {
        await _client
            .GetDatabase(_settings.DatabaseName)
            .RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
    }

    private IMongoCollection<GameSession> GameSessions =>
        _client
            .GetDatabase(_settings.DatabaseName)
            .GetCollection<GameSession>("GameSessions");

    // Save a new game or replace an existing session.
    public async Task SaveGameAsync(GameSession game)
    {
        await GameSessions.ReplaceOneAsync(
            session => session.Id == game.Id,
            game,
            new ReplaceOptions { IsUpsert = true });
    }

    // Retrieve a saved game.
    public async Task<GameSession?> GetGameAsync(string id)
    {
        return await GameSessions
            .Find(session => session.Id == id)
            .FirstOrDefaultAsync();
    }
    public Task<List<GameSession>> GetGamesForUserAsync(string userId)
    {
        return GameSessions
            .Find(game => game.OwnerUserId == userId)
            .SortByDescending(game => game.CreatedAtUtc)
            .ToListAsync();
    }
    // Update an existing game without creating a new document.
    public async Task<bool> UpdateGameAsync(GameSession game)
    {
        var previousRevision = game.Revision;
        var nextRevision = Guid.NewGuid().ToString();
        game.Revision = nextRevision;
        bool saved = false;

        try
        {
            var result = await GameSessions.ReplaceOneAsync(
                session => session.Id == game.Id && session.Revision == previousRevision,
                game,
                new ReplaceOptions { IsUpsert = false });

            saved = result.MatchedCount > 0;
            return saved;
        }
        finally
        {
            if (!saved)
                game.Revision = previousRevision;
        }
    }
}
