using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace FootballPlayerCards.Services
{
    public class AIChatService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public AIChatService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<string> GetResponseAsync(string userMessage)
        {
            var apiKey = _config["AIProvider:ApiKey"];
            var apiUrl = _config["AIProvider:ApiUrl"]!;
            var model = _config["AIProvider:Model"];

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model = model,
                messages = new[] { new { role = "user", content = userMessage } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(apiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    return $"Groq Error Details: {errorDetails}";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No response.";
            }
            catch (Exception ex)
            {
                return $"Code Error: {ex.Message}";
            }
        }
    }
}