using System.Text.Json.Serialization;

namespace AIHelloWorld.Console.Models.Gemini
{
    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; set; }
    }
    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; }
        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }
        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
    public class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; }
        [JsonPropertyName("role")]
        public string Role { get; set; }
    }
    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
