namespace PRReviewBot.Application.Models.Common
{
    public class PullRequestData
    {
        public string Owner { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public int PullRequestNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Diff { get; set; } = string.Empty;
        public string HeadSha { get; set; } = string.Empty;
    }
}
