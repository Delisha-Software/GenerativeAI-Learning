namespace AIHelloWorld.Console.Models.Common
{
    public class AIResult
    {
        public bool IsSuccess { get; init; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Model { get; set; }
        public int? InputTokes { get; set; }
        public int? OutputTokens { get; set; }
        public string? FinishReason { get; set; }
    }
}
