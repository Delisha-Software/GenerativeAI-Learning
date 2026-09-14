using Microsoft.Extensions.Options;
using PRReviewBot.Application.Interfaces;
using PRReviewBot.Application.Models.Gemini;
using PRReviewBot.Application.Services;
using PRReviewBot.Infrastructure.AI;
using PRReviewBot.Infrastructure.Configiration;
using PRReviewBot.Infrastructure.Configuration;
using PRReviewBot.Infrastructure.Factories;



// using PRReviewBot.Infrastructure.Configiration; // removed to avoid GeminiOptions ambiguity
using PRReviewBot.Infrastructure.GitHub;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<GithubOptions>()
    .Bind(builder.Configuration.GetSection(GithubOptions.SectionName))
    .Validate(o => !string.IsNullOrEmpty(o.Token), "Token is required")
    .ValidateOnStart();
builder.Services.Configure<GithubOptions>(builder.Configuration.GetSection(GithubOptions.SectionName));
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<OpenAIService>();
builder.Services.AddHttpClient<AzureOpenAIService>();
builder.Services.AddScoped<IAIServiceFactory, AIServiceFactory>();
builder.Services.AddControllers();
builder.Services.AddScoped<IPRReviewService, PRReviewService>();
builder.Services.AddHttpClient<IGitHubService, GitHubService>(
(serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<GithubOptions>>().Value;
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PRReviewBot");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
});

builder.Services.AddOptions<AIOptions>()
    .Bind(builder.Configuration.GetSection(AIOptions.SectionName))
    .Validate(o => o.Provider switch
    {
        PRReviewBot.Application.Enums.AIProvider.Gemini =>
            !string.IsNullOrWhiteSpace(o.Gemini.ApiKey) &&
            !string.IsNullOrWhiteSpace(o.Gemini.Model),
        PRReviewBot.Application.Enums.AIProvider.OpenAI =>
            !string.IsNullOrWhiteSpace(o.OpenAI.ApiKey) &&
            !string.IsNullOrWhiteSpace(o.OpenAI.Model),
        PRReviewBot.Application.Enums.AIProvider.AzureOpenAI =>
          !string.IsNullOrWhiteSpace(o.AzureOpenAI.Endpoint) &&
          !string.IsNullOrWhiteSpace(o.AzureOpenAI.ApiKey) &&
          !string.IsNullOrWhiteSpace(o.AzureOpenAI.DeploymentName),
        _ => false
    }, "Invalid AI provider or missing API key")
    .ValidateOnStart();
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection(AIOptions.SectionName));


builder.Services.AddHttpClient<GeminiService>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AIOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.Gemini.BaseUrl);
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//builder.Services.AddOptions<GeminiOptions>();
var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger UI in all environments so it's available for testing locally.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
