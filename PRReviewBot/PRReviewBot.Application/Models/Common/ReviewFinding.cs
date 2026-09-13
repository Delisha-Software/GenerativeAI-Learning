using System.Text.Json.Serialization;

namespace PRReviewBot.Application.Models.Common
{
    public class ReviewFinding
    {
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;
        [JsonPropertyName("className")]
        public string ClassName { get; set; } = string.Empty;
        [JsonPropertyName("lineNumber")]
        public int LineNumber { get; set; }
        [JsonPropertyName("side")]
        public string Side { get; set; } = "RIGHT";
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = string.Empty;
        [JsonPropertyName("issue")]
        public string Issue { get; set; } = string.Empty;
        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; }=string.Empty;
        [JsonPropertyName("possibleFix")]
        public string? SuggestedFix { get; set; }

    }
}
