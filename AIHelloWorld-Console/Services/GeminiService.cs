using System.Threading;
using System.Threading.Tasks;
using AIHelloWorld.Console.Models.Common;
using AIHelloWorld.Console.Models.Gemini;
using AIHelloWorldConsole.Configuration;
using AIHelloWorldConsole.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHelloWorldConsole.Services
{
    public class GeminiService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<GeminiOptions> _options;
        private readonly ILogger<GeminiService> _logger;
        public GeminiService(HttpClient httpClient, IOptions<GeminiOptions> geminiOptions, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _options = geminiOptions;
            _logger = logger;
        }
        public async Task<AIResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new GeminiRequest
                {
                    Contents = [

                        new Content
                    {
                        Parts = [

                            new Part { Text = prompt }

                        ]
                    }
                    ]
                };
                var jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
                var endpoint = $"v1beta/models/{_options.Value.Model}:generateContent?key={_options.Value.ApiKey}";
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Gemini API Error: {response.StatusCode} - {responseJson}");
                    throw new Exception($"Gemini API Error: {response.StatusCode} - {responseJson}");
                }
                var geminiResponse = System.Text.Json.JsonSerializer.Deserialize<GeminiResponse>(responseJson);
                if (geminiResponse is null)
                {
                    return new AIResult
                    {
                        IsSuccess = false,
                        Content = "Failed to deserialize Gemini response."
                    };
                }
                return MapToAiResult(geminiResponse);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini request was cancelled");
                return new AIResult
                {
                    IsSuccess = false,
                    Content = $"The AI request was canceled."
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex , "Network error while calling gemini");
                return new AIResult
                {
                    IsSuccess = false,
                    Content = "Unable to communicate with the AI provider"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while calling gemini");
                return new AIResult
                {
                    IsSuccess = false,
                    Content = $"An unexpected error occurred while processing the AI request"
                };
            }
        }
        private AIResult MapToAiResult(GeminiResponse Response)
        {
            var text = Response.Candidates.FirstOrDefault()?
                     .Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
            return new AIResult
            {
                IsSuccess = !string.IsNullOrWhiteSpace(text),
                Content = text
            };
        }
    }
}
