using PRReviewBot.Application.Enums;
using PRReviewBot.Infrastructure.Configuration;

namespace PRReviewBot.Infrastructure.Configiration
{
    public class AIOptions
    {
        public const string SectionName = "AI";
        public AIProvider Provider { get; set; }
        public GeminiOptions Gemini { get; set; }
        public OpenAIOptions OpenAI { get; set; }
        public AzureOpenAIOptions AzureOpenAI { get; set; }
    }
}
