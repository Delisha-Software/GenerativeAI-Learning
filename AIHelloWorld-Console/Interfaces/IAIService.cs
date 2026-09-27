using AIHelloWorld.Console.Models.Common;

namespace AIHelloWorldConsole.Interfaces
{
    public interface IAIService
    {
        Task<AIResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
