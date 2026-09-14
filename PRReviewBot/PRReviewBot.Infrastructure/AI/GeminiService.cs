using Microsoft.Extensions.Options;
using PRReviewBot.Application;
using PRReviewBot.Application.Interfaces;
using PRReviewBot.Application.Models.Common;
using PRReviewBot.Application.Models.Gemini;
using PRReviewBot.Infrastructure.Configiration;
using PRReviewBot.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace PRReviewBot.Infrastructure.AI;

public sealed class GeminiService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiService(
    HttpClient httpClient,
    IOptions<AIOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value.Gemini;
    }

    public async Task<AIReviewResponse> ReviewCodeAsync(
    PullRequestData pullRequest,
    CancellationToken cancellationToken = default)
    {
        var prompt = AIReviewPromptBuilder.BuildReviewPrompt(pullRequest);

        var requestBody = new GeminiRequest
        {
            Contents = new System.Collections.Generic.List<PRReviewBot.Application.Models.Gemini.Content>
                    {
                        new PRReviewBot.Application.Models.Gemini.Content
                        {
                            Parts = new System.Collections.Generic.List<PRReviewBot.Application.Models.Gemini.Part>
                            {
                                new PRReviewBot.Application.Models.Gemini.Part { Text = prompt }
                            }
                        }
                    }
        };

        var endpoint =
        $"v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        using var response = await _httpClient.PostAsJsonAsync(
        endpoint,
        requestBody,
        cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(
        cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
            $"Gemini API failed. Status: {response.StatusCode}. " +
            $"Response: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);

        var responseText = document.RootElement
        .GetProperty("candidates")[0]
        .GetProperty("content")
        .GetProperty("parts")[0]
        .GetProperty("text")
        .GetString();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException(
            "Gemini did not return review content.");
        }

        return AIResponseParser.Parse(responseText);
    }
}
