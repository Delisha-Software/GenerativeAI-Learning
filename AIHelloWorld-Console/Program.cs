using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AIHelloWorldConsole.Interfaces;
using AIHelloWorldConsole.Services;
using AIHelloWorldConsole.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;

// Use Generic Host
var builder = Host.CreateApplicationBuilder(args);

// Bind and validate GeminiOptions from configuration. ValidateOnStart will throw if required values are missing.
builder.Services.AddOptions<GeminiOptions>()
    .Bind(builder.Configuration.GetSection(GeminiOptions.SectionName))
    .Validate(o => !string.IsNullOrEmpty(o.ApiKey), "ApiKey is required")
    .ValidateOnStart();

builder.Services.AddHttpClient<IAIService, GeminiService>((serviceProvider, client) =>
 {
     var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
     client.BaseAddress = new Uri(options.BaseUrl);
 })
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
});
var app = builder.Build();
var aiService = app.Services.GetRequiredService<IAIService>();
Console.Write("Ask AI:");
var prompt = Console.ReadLine();

if (!string.IsNullOrWhiteSpace(prompt))
{
    //using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var response = await aiService.GenerateAsync(prompt);
    var result = response.IsSuccess ? response.Content : $"Error: {response.Content}";
    Console.WriteLine();
    Console.WriteLine($"AI Response:");
    Console.WriteLine("----------------");
    Console.WriteLine(result);

}

