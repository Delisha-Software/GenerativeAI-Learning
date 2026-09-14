using Microsoft.Extensions.Options;
using PRReviewBot.Application.Enums;
using PRReviewBot.Application.Interfaces;
using PRReviewBot.Infrastructure.AI;
using PRReviewBot.Infrastructure.Configiration;

namespace PRReviewBot.Infrastructure.Factories;

public sealed class AIServiceFactory : IAIServiceFactory
{
    private readonly AIOptions _options;
    private readonly GeminiService _geminiService;
    private readonly OpenAIService _openAIService;
    private readonly AzureOpenAIService _azureOpenAIService;

    public AIServiceFactory(
    IOptions<AIOptions> options,
    GeminiService geminiService,
    OpenAIService openAIService,
    AzureOpenAIService azureOpenAIService)
    {
        _options = options.Value;
        _geminiService = geminiService;
        _openAIService = openAIService;
        _azureOpenAIService = azureOpenAIService;
    }

    public IAIService Create()
    {
        return _options.Provider switch
        {
            AIProvider.Gemini => _geminiService,

            AIProvider.OpenAI => _openAIService,

            AIProvider.AzureOpenAI => _azureOpenAIService,

            _ => throw new InvalidOperationException(
            $"Unsupported AI provider: {_options.Provider}")
        };
    }
}
