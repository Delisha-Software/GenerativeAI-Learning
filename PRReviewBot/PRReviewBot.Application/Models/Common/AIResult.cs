using System.Security.Principal;

namespace PRReviewBot.Application.Models.Common
{
    public class AIResult
    {
        public bool IsSuccess { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}
