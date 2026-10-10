
using Microsoft.Extensions.Options;
using PRReviewBot.Application;
using PRReviewBot.Application.Interfaces;
using PRReviewBot.Application.Models.Common;
using PRReviewBot.Infrastructure.Configiration;
using System.Net.Http.Json;
using System.Text.Json;

namespace PRReviewBot.Infrastructure.AI;

public sealed class AzureOpenAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAIOptions _options;

    public AzureOpenAIService(
    HttpClient httpClient,
    IOptions<AIOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value.AzureOpenAI;
    }

    public async Task<AIReviewResponse> ReviewCodeAsync(
    PullRequestData pullRequest,
    CancellationToken cancellationToken = default)
    {
        var prompt = AIReviewPromptBuilder.BuildReviewPrompt(pullRequest);

        var endpoint =
        $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/" +
        $"{_options.DeploymentName}/chat/completions" +
        $"?api-version={_options.ApiVersion}";

        using var request = new HttpRequestMessage(
        HttpMethod.Post,
        endpoint);

        request.Headers.Add("api-key", _options.ApiKey);

        request.Content = JsonContent.Create(new
        {
            temperature = 0.1,
            messages = new[]
        {
new
{
role = "system",
content = "You are an expert software code reviewer."
},
new
{
role = "user",
content = prompt
}
}
        });

        using var response = await _httpClient.SendAsync(
        request,
        cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(
        cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
            $"Azure OpenAI API failed. Status: {response.StatusCode}. " +
            $"Response: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);

        var responseText = document.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException(
            "Azure OpenAI did not return review content.");
        }

        return AIResponseParser.Parse(responseText);
    }
}

