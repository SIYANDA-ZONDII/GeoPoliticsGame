using GeopoliticsGame.Models;
using GeopoliticsGame.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GeopoliticsGame.Services;

public class AccountService
{
    private readonly IMongoCollection<AppUser> _users;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AccountService(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        _users = client
            .GetDatabase(settings.Value.DatabaseName)
            .GetCollection<AppUser>("Users");
    }

    public async Task<AppUser> RegisterAsync(string username, string password)
    {
        var usernameIndex = new CreateIndexModel<AppUser>(
            Builders<AppUser>.IndexKeys.Ascending(user => user.Username),
            new CreateIndexOptions { Unique = true });

        await _users.Indexes.CreateOneAsync(usernameIndex);

        var user = new AppUser
        {
            Username = username.Trim().ToUpperInvariant()
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        try
        {
            await _users.InsertOneAsync(user);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException(
                "That username is already registered.", ex);
        }

        return user;
    }

    public async Task<AppUser?> LoginAsync(string username, string password)
    {
        string normalizedUsername = username.Trim().ToUpperInvariant();

        var user = await _users
            .Find(account => account.Username == normalizedUsername)
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, password);

        return result == PasswordVerificationResult.Failed ? null : user;
    }
}