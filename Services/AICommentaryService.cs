using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GeopoliticsGame.Services;

public class AICommentaryService(HttpClient http, IConfiguration configuration, ILogger<AICommentaryService> logger)
{
    public async Task<string> GenerateCommentaryAsync(string attacker, string defender, string result)
    {
        const string fallback = "The battle has concluded. The world awaits the consequences.";
        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434/api/chat";
        var model = configuration["Ollama:Model"] ?? "llama3.2:3b";
        var apiKey = configuration["Ollama:ApiKey"];
        var groqKey = configuration["Groq:ApiKey"];

        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            endpoint = "https://api.groq.com/openai/v1/chat/completions";
            model = "openai/gpt-oss-20b";
            apiKey = groqKey;
        }

        var prompt = $"Write two short sentences of strategy-game battle commentary. " +
            $"Attacker: {attacker}. Defender: {defender}. Actual result: {result}. " +
            "Keep the facts accurate and do not invent details.";

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    model,
                    messages = new[] { new { role = "user", content = prompt } },
                    stream = false
                })
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            using var response = await http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<CommentaryResponse>(cancellationToken: timeout.Token);
            var commentary = data?.Message?.Content ?? data?.Choices?.FirstOrDefault()?.Message?.Content;

            if (!string.IsNullOrWhiteSpace(commentary))
            {
                return commentary.Trim();
            }

            logger.LogWarning("Commentary API returned empty battle commentary.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Commentary API request failed. HTTP status: {StatusCode}; error: {Error}.",
                ex.StatusCode, ex.HttpRequestError);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Commentary API request timed out.");
        }
        catch (JsonException)
        {
            logger.LogWarning("Commentary API returned an invalid commentary response.");
        }

        return fallback;
    }

    private sealed record CommentaryResponse(ChatMessage? Message, ChatChoice[]? Choices);
    private sealed record ChatChoice(ChatMessage? Message);
    private sealed record ChatMessage(string? Content);
}
