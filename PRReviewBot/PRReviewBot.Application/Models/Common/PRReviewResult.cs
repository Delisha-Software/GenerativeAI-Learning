namespace PRReviewBot.Application.Models.Common
{
    public class PRReviewResult
    {
        public string Summary { get; set; } = string.Empty;
        public List<ReviewFinding> Findings { get; set; } = [];
    }
}
