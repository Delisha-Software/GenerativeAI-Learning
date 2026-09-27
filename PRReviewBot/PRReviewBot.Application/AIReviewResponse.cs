using PRReviewBot.Application.Models.Common;

namespace PRReviewBot.Application;

public class AIReviewResponse
{
    public List<ReviewFinding> Findings { get; set; } = new List<ReviewFinding>();
}