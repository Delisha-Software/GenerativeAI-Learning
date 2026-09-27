namespace PRReviewBot.Infrastructure.Configuration
{
    public class GeminiOptions
    {
        // Configuration section name used in Program.cs
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-1.0";
        public string BaseUrl { get; set; } = "https://api.generativeai.google.com/";
    }
}
