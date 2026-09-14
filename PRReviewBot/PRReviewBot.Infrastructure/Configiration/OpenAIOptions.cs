namespace PRReviewBot.Infrastructure.Configiration
{
    public class OpenAIOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4-mini";
        public string BaseUrl { get; set; }
    }
}
