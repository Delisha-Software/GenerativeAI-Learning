namespace PRReviewBot.Infrastructure.Configiration
{
    public class GithubOptions
    {
        public const string SectionName = "Github";
        public string Token { get; set; } = string.Empty;
    }
}
