namespace AIHelloWorldConsole.Configuration
{
    public class GeminiOptions
    {
        // Configuration section name used in Program
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; }
        public string Model { get; set; }
        public string BaseUrl { get; set; }
    }
}
