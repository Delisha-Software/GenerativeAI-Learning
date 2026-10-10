using PRReviewBot.Application;
using System.Text.Json;

namespace PRReviewBot.Infrastructure.AI;

public static class AIResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AIReviewResponse Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException(
            "The AI provider returned an empty response.");
        }

        var cleanedResponse = RemoveMarkdownCodeFence(responseText);

        var result = JsonSerializer.Deserialize<AIReviewResponse>(
        cleanedResponse,
        JsonOptions);

        return result ?? new AIReviewResponse();
    }

    private static string RemoveMarkdownCodeFence(string responseText)
    {
        var result = responseText.Trim();

        if (result.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            result = result["```json".Length..].Trim();
        }
        else if (result.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            result = result["```".Length..].Trim();
        }

        if (result.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            result = result[..^3].Trim();
        }

        return result;
    }
}

