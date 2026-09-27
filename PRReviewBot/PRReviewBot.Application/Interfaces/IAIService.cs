using PRReviewBot.Application.Models.Common;

namespace PRReviewBot.Application.Interfaces
{
    public interface IAIService
    {
        Task<AIReviewResponse> ReviewCodeAsync(PullRequestData pullRequest, CancellationToken cancellationToken = default);
    }
}
