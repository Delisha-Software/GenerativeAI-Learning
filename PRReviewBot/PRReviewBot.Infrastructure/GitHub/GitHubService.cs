using PRReviewBot.Application.Interfaces;
using PRReviewBot.Application.Models;
using PRReviewBot.Application.Models.Common;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PRReviewBot.Infrastructure.GitHub;

public sealed class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public GitHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<PullRequestData> GetPullRequestAsync(
    string pullRequestUrl,
    CancellationToken cancellationToken)
    {
        var uri = new Uri(pullRequestUrl);

        var segments = uri.AbsolutePath
        .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 4 ||
        !segments[2].Equals("pull", StringComparison.OrdinalIgnoreCase) &&
        !segments[2].Equals("pulls", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
            "Invalid GitHub pull request URL.",
            nameof(pullRequestUrl));
        }

        var owner = segments[0];
        var repository = segments[1];
        var pullNumber = int.Parse(segments[3]);

        var endpoint =
        $"repos/{owner}/{repository}/pulls/{pullNumber}";

        using var response = await _httpClient.GetAsync(
        endpoint,
        cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(
        cancellationToken);

        var githubResponse =
        JsonSerializer.Deserialize<GitHubPullRequestResponse>(
        json,
        _jsonOptions)
        ?? throw new InvalidOperationException(
        "Unable to deserialize GitHub PR response.");

        if (string.IsNullOrWhiteSpace(githubResponse.Head.Sha))
        {
            throw new InvalidOperationException(
            "GitHub PR head SHA was not returned.");
        }

        var diffEndpoint =
        $"repos/{owner}/{repository}/pulls/{pullNumber}";

        using var diffRequest = new HttpRequestMessage(
        HttpMethod.Get,
        diffEndpoint);

        diffRequest.Headers.Accept.ParseAdd(
        "application/vnd.github.v3.diff");

        using var diffResponse = await _httpClient.SendAsync(
        diffRequest,
        cancellationToken);

        diffResponse.EnsureSuccessStatusCode();

        var diff = await diffResponse.Content.ReadAsStringAsync(
        cancellationToken);

        return new PullRequestData
        {
            Owner = owner,
            Repository = repository,
            PullRequestNumber = pullNumber,
            HeadSha = githubResponse.Head.Sha,
            Diff = diff
        };
    }

    public async Task AddReviewCommentsAsync(
    PullRequestData pullRequest,
    IEnumerable<ReviewFinding> findings,
    CancellationToken cancellationToken)
    {
        var validFindings = findings
        .Where(IsValidFinding)
        .ToList();

        if (validFindings.Count == 0)
        {
            return;
        }

        var existingComments =
        await GetExistingCommentsAsync(
        pullRequest,
        cancellationToken);

        var existingKeys = existingComments
        .Where(IsBotComment)
        .Select(comment => CreateExistingCommentKey(
        comment,
        pullRequest.HeadSha))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var commentsToPost = new List<GitHubReviewComment>();

        foreach (var finding in validFindings)
        {
            var key = CreateFindingKey(
            finding,
            pullRequest.HeadSha);

            if (existingKeys.Contains(key))
            {
                continue;
            }

            commentsToPost.Add(new GitHubReviewComment
            {
                Body = BuildReviewComment(finding),
                Path = finding.FilePath,
                Line = finding.LineNumber,
                Side = finding.Side
            });

            existingKeys.Add(key);
        }

        if (commentsToPost.Count == 0)
        {
            return;
        }

        var reviewRequest = new GitHubReviewRequest
        {
            CommitId = pullRequest.HeadSha,
            Event = "COMMENT",
            Body = "🤖 AI Code Review",
            Comments = commentsToPost
        };

        using var response = await _httpClient.PostAsJsonAsync(
        $"repos/{pullRequest.Owner}/{pullRequest.Repository}" +
        $"/pulls/{pullRequest.PullRequestNumber}/reviews",
        reviewRequest,
        _jsonOptions,
        cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(
            cancellationToken);

            throw new HttpRequestException(
            $"GitHub review creation failed. " +
            $"Status: {response.StatusCode}. " +
            $"Details: {error}");
        }
    }

    private async Task<List<GitHubExistingComment>>
    GetExistingCommentsAsync(
    PullRequestData pullRequest,
    CancellationToken cancellationToken)
    {
        var endpoint =
        $"repos/{pullRequest.Owner}/{pullRequest.Repository}" +
        $"/pulls/{pullRequest.PullRequestNumber}/comments";

        using var response = await _httpClient.GetAsync(
        endpoint,
        cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(
        cancellationToken);

        return JsonSerializer.Deserialize<
        List<GitHubExistingComment>>(
        json,
        _jsonOptions) ?? new List<GitHubExistingComment>();
    }

    private static bool IsValidFinding(ReviewFinding finding)
    {
        return !string.IsNullOrWhiteSpace(finding.FilePath)
        && finding.LineNumber > 0
        && !string.IsNullOrWhiteSpace(finding.Issue);
    }

    private static bool IsBotComment(
    GitHubExistingComment comment)
    {
        return comment.Body.Contains(
        "PRReviewBot",
        StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateFindingKey(
    ReviewFinding finding,
    string headSha)
    {
        return string.Join(
        "|",
        headSha.Trim().ToLowerInvariant(),
        finding.FilePath.Trim().ToLowerInvariant(),
        finding.LineNumber,
        finding.Issue.Trim().ToLowerInvariant());
    }

    private static string CreateExistingCommentKey(
    GitHubExistingComment comment,
    string headSha)
    {
        return string.Join(
        "|",
        headSha.Trim().ToLowerInvariant(),
        comment.Path.Trim().ToLowerInvariant(),
        comment.Line ?? 0,
        ExtractIssue(comment.Body).Trim().ToLowerInvariant());
    }

    private static string ExtractIssue(string body)
    {
        const string start = "**Issue:**";

        var index = body.IndexOf(
        start,
        StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return body;
        }

        var issue = body[(index + start.Length)..];

        var recommendationIndex = issue.IndexOf(
        "**Recommendation:**",
        StringComparison.OrdinalIgnoreCase);

        if (recommendationIndex >= 0)
        {
            issue = issue[..recommendationIndex];
        }

        return issue.Trim();
    }

    private static string BuildReviewComment(
    ReviewFinding finding)
    {
        return $"""
<!-- PRReviewBot -->

🤖 **AI Code Review — {finding.Severity}**

**Class:** {finding.ClassName}

**Issue:**
{finding.Issue}

**Recommendation:**
{finding.Recommendation}

**Suggested Fix:**
{finding.SuggestedFix ?? "No specific fix suggested."}

_Generated by PRReviewBot_
""";
    }


    // =================================================
    // GitHub Response Models
    // =================================================

    private sealed class GitHubPullRequestResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("head")]
        public GitHubPullRequestHead Head { get; set; }
    }

    private sealed class GitHubPullRequestHead
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; } = string.Empty;

        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;
    }

    // =================================================
    // GitHub Review Request
    // =================================================

    private sealed class GitHubReviewRequest
    {
        [JsonPropertyName("commit_id")]
        public string CommitId { get; set; } = string.Empty;
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
        [JsonPropertyName("event")]
        public string Event { get; set; } = "COMMENT";
        [JsonPropertyName("comments")]
        public List<GitHubReviewComment> Comments { get; set; } = new List<GitHubReviewComment>();
    }

    // =================================================
    // GitHub Review Comment
    // =================================================

    private sealed class GitHubReviewComment
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
        [JsonPropertyName("line")]
        public int Line { get; set; }
        [JsonPropertyName("side")]
        public string Side { get; set; } = "RIGHT";
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }
    private sealed class GitHubExistingComment
    {
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
        [JsonPropertyName("line")]
        public int? Line { get; set; }
        [JsonPropertyName("side")]
        public string? Side { get; set; }
        [JsonPropertyName("commit_id")]
        public string CommitId { get; set; } = string.Empty;

    }
    private sealed record PullRequestInfo(
    string Owner,
    string Repository,
    int Number);
}
