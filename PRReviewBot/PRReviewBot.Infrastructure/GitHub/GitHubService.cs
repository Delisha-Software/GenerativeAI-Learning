using PRReviewBot.Application.Interfaces;
using PRReviewBot.Application.Models;
using PRReviewBot.Application.Models.Common;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    // ============================================================
    // Get pull request metadata and diff
    // ============================================================

    public async Task<PullRequestData> GetPullRequestAsync(
    string pullRequestUrl,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
        {
            throw new ArgumentException(
            "GitHub pull request URL is required.",
            nameof(pullRequestUrl));
        }

        if (!Uri.TryCreate(
        pullRequestUrl,
        UriKind.Absolute,
        out var uri))
        {
            throw new ArgumentException(
            "Invalid GitHub pull request URL.",
            nameof(pullRequestUrl));
        }

        var segments = uri.AbsolutePath.Split(
        '/',
        StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 4 ||
        (!segments[2].Equals(
        "pull",
        StringComparison.OrdinalIgnoreCase) &&
        !segments[2].Equals(
        "pulls",
        StringComparison.OrdinalIgnoreCase)) ||
        !int.TryParse(
        segments[3],
        out var pullRequestNumber))
        {
            throw new ArgumentException(
            "Invalid GitHub pull request URL. " +
            "Expected format: " +
            "https://github.com/{owner}/{repository}/pull/{number}",
            nameof(pullRequestUrl));
        }

        var owner = Uri.UnescapeDataString(segments[0]);
        var repository = Uri.UnescapeDataString(segments[1]);

        var pullRequestEndpoint =
        $"repos/{owner}/{repository}/pulls/{pullRequestNumber}";

        // --------------------------------------------------------
        // Get PR metadata
        // --------------------------------------------------------

        using var pullRequestResponse =
        await SendGetRequestAsync(
        pullRequestEndpoint,
        "application/vnd.github+json",
        cancellationToken);

        var pullRequestJson =
        await pullRequestResponse.Content.ReadAsStringAsync(
        cancellationToken);

        var githubPullRequest =
        JsonSerializer.Deserialize<GitHubPullRequestResponse>(
        pullRequestJson,
        _jsonOptions);

        if (githubPullRequest is null)
        {
            throw new InvalidOperationException(
            "Unable to deserialize the GitHub pull request response.");
        }

        if (githubPullRequest.Head is null ||
        string.IsNullOrWhiteSpace(
        githubPullRequest.Head.Sha))
        {
            throw new InvalidOperationException(
            "GitHub did not return a valid pull request head SHA.");
        }

        var headSha = githubPullRequest.Head.Sha;

        // --------------------------------------------------------
        // Get PR diff
        // --------------------------------------------------------

        using var diffResponse =
        await SendGetRequestAsync(
        pullRequestEndpoint,
        "application/vnd.github.v3.diff",
        cancellationToken);

        var diff =
        await diffResponse.Content.ReadAsStringAsync(
        cancellationToken);

        return new PullRequestData
        {
            Owner = owner,
            Repository = repository,
            PullRequestNumber = pullRequestNumber,
            HeadSha = headSha,
            Diff = diff
        };
    }

    // ============================================================
    // Add inline review comments
    // ============================================================

    public async Task AddReviewCommentsAsync(
    PullRequestData pullRequest,
    IEnumerable<ReviewFinding> findings,
    CancellationToken cancellationToken = default)
    {
        if (pullRequest is null)
        {
            throw new ArgumentNullException(nameof(pullRequest));
        }

        if (string.IsNullOrWhiteSpace(pullRequest.Owner))
        {
            throw new ArgumentException(
            "Pull request owner is missing.",
            nameof(pullRequest));
        }

        if (string.IsNullOrWhiteSpace(pullRequest.Repository))
        {
            throw new ArgumentException(
            "Pull request repository is missing.",
            nameof(pullRequest));
        }

        if (pullRequest.PullRequestNumber <= 0)
        {
            throw new ArgumentException(
            "Pull request number is invalid.",
            nameof(pullRequest));
        }

        if (string.IsNullOrWhiteSpace(pullRequest.HeadSha))
        {
            throw new InvalidOperationException(
            "Pull request head SHA is empty.");
        }

        if (findings is null)
        {
            return;
        }

        var validFindings = findings
        .Where(IsValidFinding)
        .ToList();

        if (validFindings.Count == 0)
        {
            return;
        }

        // --------------------------------------------------------
        // Get ALL existing PR review comments
        // --------------------------------------------------------

        var existingComments =
        await GetExistingCommentsAsync(
        pullRequest,
        cancellationToken);

        var newComments =
        new List<GitHubReviewCommentRequest>();

        // --------------------------------------------------------
        // Prevent duplicate findings from Gemini response
        // --------------------------------------------------------

        var processedFindingKeys =
        new HashSet<string>(
        StringComparer.OrdinalIgnoreCase);

        // --------------------------------------------------------
        // Process findings
        // --------------------------------------------------------

        foreach (var finding in validFindings)
        {
            // Stable key based on FILE + LINE.
            //
            // IMPORTANT:
            // Do NOT include:
            // - HEAD SHA
            // - AI issue text
            //
            // because both can change between reviews.
            var findingKey =
            CreateFindingKey(finding);

            // ----------------------------------------------------
            // Duplicate inside the SAME AI response
            // ----------------------------------------------------

            if (!processedFindingKeys.Add(findingKey))
            {
                continue;
            }

            // ----------------------------------------------------
            // Duplicate already posted to GitHub
            // ----------------------------------------------------

            var commentAlreadyExists =
            existingComments.Any(existingComment =>
            IsSameBotComment(
            existingComment,
            finding,
            findingKey));

            if (commentAlreadyExists)
            {
                continue;
            }

            // ----------------------------------------------------
            // Create new GitHub comment
            // ----------------------------------------------------

            newComments.Add(
            new GitHubReviewCommentRequest
            {
                Body = BuildReviewComment(
            finding,
            findingKey),

                Path = finding.FilePath.Trim(),

                Line = finding.LineNumber,

                Side = NormalizeSide(finding.Side)
            });
        }

        // --------------------------------------------------------
        // Nothing new to post
        // --------------------------------------------------------

        if (newComments.Count == 0)
        {
            return;
        }

        // --------------------------------------------------------
        // Create ONE GitHub review containing all comments
        // --------------------------------------------------------

        var reviewRequest =
        new GitHubReviewRequest
        {
            Body = "🤖 AI Code Review",

            CommitId = pullRequest.HeadSha,

            Event = "COMMENT",

            Comments = newComments
        };

        var endpoint =
        $"repos/{pullRequest.Owner}/" +
        $"{pullRequest.Repository}/" +
        $"pulls/{pullRequest.PullRequestNumber}/reviews";

        var requestJson =
        JsonSerializer.Serialize(
        reviewRequest,
        _jsonOptions);

        using var request =
        new HttpRequestMessage(
        HttpMethod.Post,
        endpoint);

        request.Headers.Accept.Clear();

        request.Headers.Accept.Add(
        new MediaTypeWithQualityHeaderValue(
        "application/vnd.github+json"));

        request.Content =
        new StringContent(
        requestJson,
        Encoding.UTF8,
        "application/json");

        using var response =
        await _httpClient.SendAsync(
        request,
        cancellationToken);

        var responseBody =
        await response.Content.ReadAsStringAsync(
        cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
            $"GitHub review creation failed. " +
            $"Status: {(int)response.StatusCode} " +
            $"{response.ReasonPhrase}. " +
            $"Response: {responseBody}");
        }
    }

    // ============================================================
    // Determine whether existing comment belongs to this bot
    // ============================================================

    private static bool IsSameBotComment(
    GitHubExistingComment existingComment,
    ReviewFinding finding,
    string findingKey)
    {
        if (string.IsNullOrWhiteSpace(
        existingComment.Body))
        {
            return false;
        }

        // --------------------------------------------------------
        // 1. Check our exact fingerprint marker
        // --------------------------------------------------------

        var marker =
        $"PRReviewBot-Fingerprint:{findingKey}";

        if (existingComment.Body.Contains(
        marker,
        StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // --------------------------------------------------------
        // 2. Ignore comments that are not created by our bot
        // --------------------------------------------------------

        var isOurComment =
        existingComment.Body.Contains(
        "🤖 **AI Code Review**",
        StringComparison.OrdinalIgnoreCase);

        if (!isOurComment)
        {
            return false;
        }

        // --------------------------------------------------------
        // 3. Same file?
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
        existingComment.Path))
        {
            return false;
        }

        if (!string.Equals(
        existingComment.Path.Trim(),
        finding.FilePath.Trim(),
        StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // --------------------------------------------------------
        // 4. Determine GitHub line
        // --------------------------------------------------------

        var existingLine =
        existingComment.Line ??
        existingComment.OriginalLine;

        if (!existingLine.HasValue)
        {
            return false;
        }

        // --------------------------------------------------------
        // 5. Allow small line movement
        //
        // Example:
        //
        // Previous comment -> line 34
        // Current finding -> line 35
        //
        // Still considered the same location.
        // --------------------------------------------------------

        var lineDifference =
        Math.Abs(
        existingLine.Value -
        finding.LineNumber);

        if (lineDifference <= 2)
        {
            return true;
        }

        return false;
    }

    // ============================================================
    // Get existing PR review comments
    // ============================================================

    private async Task<List<GitHubExistingComment>>
    GetExistingCommentsAsync(
    PullRequestData pullRequest,
    CancellationToken cancellationToken)
    {
        var allComments =
        new List<GitHubExistingComment>();

        const int pageSize = 100;

        var pageNumber = 1;

        while (true)
        {
            var endpoint =
            $"repos/{pullRequest.Owner}/" +
            $"{pullRequest.Repository}/" +
            $"pulls/{pullRequest.PullRequestNumber}/comments" +
            $"?per_page={pageSize}&page={pageNumber}";

            using var response =
            await SendGetRequestAsync(
            endpoint,
            "application/vnd.github+json",
            cancellationToken);

            var json =
            await response.Content.ReadAsStringAsync(
            cancellationToken);

            var pageComments =
            JsonSerializer.Deserialize<
            List<GitHubExistingComment>>(
            json,
            _jsonOptions)
            ?? new List<GitHubExistingComment>();

            if (pageComments.Count == 0)
            {
                break;
            }

            allComments.AddRange(pageComments);

            if (pageComments.Count < pageSize)
            {
                break;
            }

            pageNumber++;
        }

        return allComments;
    }

    // ============================================================
    // Send GET request
    // ============================================================

    private async Task<HttpResponseMessage>
    SendGetRequestAsync(
    string endpoint,
    string acceptHeader,
    CancellationToken cancellationToken)
    {
        using var request =
        new HttpRequestMessage(
        HttpMethod.Get,
        endpoint);

        request.Headers.Accept.Clear();

        request.Headers.Accept.Add(
        new MediaTypeWithQualityHeaderValue(
        acceptHeader));

        var response =
        await _httpClient.SendAsync(
        request,
        cancellationToken);

        var responseBody =
        await response.Content.ReadAsStringAsync(
        cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();

            throw new HttpRequestException(
            $"GitHub GET request failed. " +
            $"Status: {(int)response.StatusCode} " +
            $"{response.ReasonPhrase}. " +
            $"Response: {responseBody}");
        }

        return response;
    }

    // ============================================================
    // Validate finding
    // ============================================================

    private static bool IsValidFinding(
    ReviewFinding finding)
    {
        if (finding is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
        finding.FilePath))
        {
            return false;
        }

        if (finding.LineNumber <= 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
        finding.Issue))
        {
            return false;
        }

        return true;
    }

    // ============================================================
    // Normalize GitHub side
    // ============================================================

    private static string NormalizeSide(
    string? side)
    {
        return string.Equals(
        side,
        "LEFT",
        StringComparison.OrdinalIgnoreCase)
        ? "LEFT"
        : "RIGHT";
    }

    // ============================================================
    // Create stable finding key
    // ============================================================

    private static string CreateFindingKey(
    ReviewFinding finding)
    {
        var canonicalValue =
        $"{finding.FilePath.Trim().ToLowerInvariant()}|" +
        $"{finding.LineNumber}";

        var inputBytes =
        Encoding.UTF8.GetBytes(
        canonicalValue);

        var hashBytes =
        SHA256.HashData(inputBytes);

        return Convert.ToHexString(
        hashBytes);
    }

    // ============================================================
    // Build review comment
    // ============================================================

    private static string BuildReviewComment(
    ReviewFinding finding,
    string findingKey)
    {
        return $"""
<!-- PRReviewBot -->
<!-- PRReviewBot-Fingerprint:{findingKey} -->

🤖 **AI Code Review**

**Severity:** {finding.Severity}

**Class:** {finding.ClassName}

**Issue:**
{finding.Issue}

**Recommendation:**
{finding.Recommendation}

**Suggested Fix:**
{finding.SuggestedFix ?? "No specific fix suggested."}

---
Generated by PRReviewBot
""";
    }

    // ============================================================
    // GitHub response models
    // ============================================================

    private sealed class GitHubPullRequestResponse
    {
        [JsonPropertyName("head")]
        public GitHubPullRequestHead? Head { get; set; }
    }

    private sealed class GitHubPullRequestHead
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }

    // ============================================================
    // GitHub review request
    // ============================================================

    private sealed class GitHubReviewRequest
    {
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("commit_id")]
        public string CommitId { get; set; } = string.Empty;

        [JsonPropertyName("event")]
        public string Event { get; set; } = "COMMENT";

        [JsonPropertyName("comments")]
        public List<GitHubReviewCommentRequest> Comments { get; set; } = [];
    }

    // ============================================================
    // GitHub review comment request
    // ============================================================

    private sealed class GitHubReviewCommentRequest
    {
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("side")]
        public string Side { get; set; } = "RIGHT";
    }

    // ============================================================
    // Existing GitHub comment
    // ============================================================

    private sealed class GitHubExistingComment
    {
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("line")]
        public int? Line { get; set; }

        [JsonPropertyName("original_line")]
        public int? OriginalLine { get; set; }

        [JsonPropertyName("side")]
        public string? Side { get; set; }

        [JsonPropertyName("commit_id")]
        public string? CommitId { get; set; }
    }
}
