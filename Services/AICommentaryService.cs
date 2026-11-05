
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GeopoliticsGame.Services
{
    public class AICommentaryService
    {
        private readonly HttpClient _http;

        public AICommentaryService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> GenerateCommentaryAsync(
            string attacker,
            string defender,
            string result)
        {
            try
            {
                var prompt =
                    $"You are a geopolitical war commentator. " +
                    $"Write 2 short dramatic sentences about this battle. " +
                    $"Attacker: {attacker}. " +
                    $"Defender: {defender}. " +
                    $"Actual result: {result}. " +
                    $"Do not change or invent the result.";

                var response = await _http.PostAsJsonAsync(
                    "http://localhost:11434/api/generate",
                    new
                    {
                        model = "llama3.2:3b",
                        prompt,
                        stream = false
                    });

                response.EnsureSuccessStatusCode();

                var data = await response.Content
                    .ReadFromJsonAsync<OllamaResponse>();

                return data?.Response ?? "Battle commentary unavailable.";
            }
            catch
            {
                return "The battle has concluded. The world awaits the consequences.";
            }
        }

        private class OllamaResponse
        {
            [JsonPropertyName("response")]
            public string? Response { get; set; }
        }
    }
}
