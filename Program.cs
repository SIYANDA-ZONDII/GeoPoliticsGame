using GeopoliticsGame.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<AccountService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => options.LoginPath = "/Account");
builder.Services.AddSingleton<GeopoliticsGame.Services.GameInitializationService>();
builder.Services.AddSingleton<GeopoliticsGame.Services.GameSessionStore>();
builder.Services.Configure<GeopoliticsGame.Settings.MongoDbSettings>(
builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<MongoDB.Driver.IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider
        .GetRequiredService<
            Microsoft.Extensions.Options.IOptions<
                GeopoliticsGame.Settings.MongoDbSettings>>()
        .Value;

    if (string.IsNullOrWhiteSpace(settings.ConnectionString))
    {
        throw new InvalidOperationException(
            "MongoDB connection string is missing. Check .NET User Secrets.");
    }

    return new MongoDB.Driver.MongoClient(settings.ConnectionString);
     });
builder.Services.AddSingleton<GeopoliticsGame.Services.MongoDbConnectionService>();
builder.Services.AddScoped<GeopoliticsGame.Services.GameStateService>();
builder.Services.AddSingleton<GeopoliticsGame.Services.NpcDecisionService>();
builder.Services.AddSingleton<GeopoliticsGame.Services.BattleService>();
builder.Services.AddHttpClient<AICommentaryService>();
var app = builder.Build();
var mongoSettings = app.Services
    .GetRequiredService<
        Microsoft.Extensions.Options.IOptions<
            GeopoliticsGame.Settings.MongoDbSettings>>()
    .Value;

try
{
    var uri = new Uri(mongoSettings.ConnectionString
        .Replace("mongodb+srv://", "https://")
        .Replace("mongodb://", "https://"));

    Console.WriteLine(
        $"MongoDB hostname loaded: {uri.Host}");
}
catch
{
    Console.WriteLine(
        "Could not parse the MongoDB connection string.");
}
using (var scope = app.Services.CreateScope())
{
    try
    {
        var mongoService = scope.ServiceProvider
            .GetRequiredService<
                GeopoliticsGame.Services.MongoDbConnectionService>();

        await mongoService.TestConnectionAsync();

        Console.WriteLine("MongoDB connection successful.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("MongoDB connection failed:");
        Console.WriteLine(ex.GetBaseException().Message);
    }
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
