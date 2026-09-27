using PRReviewBot.Application.Models.Common;

namespace PRReviewBot.Application.Interfaces
{
    public interface IGitHubService
    {
        Task<PullRequestData> GetPullRequestAsync(string pullRequestUrl, 
            CancellationToken cancellationToken = default);
        Task AddReviewCommentsAsync(PullRequestData pullRequestData,
            IEnumerable<ReviewFinding> findings,
            CancellationToken cancellationToken);
    }
}




























