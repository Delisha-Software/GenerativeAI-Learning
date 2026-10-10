using PRReviewBot.Application.Interfaces;
using PRReviewBot.Application.Models.Common;
using System.Text.Json;
using System.Text;

namespace PRReviewBot.Application.Services
{
    public class PRReviewService : IPRReviewService
    {
        private readonly IAIServiceFactory aIServiceFactory;
        private readonly IGitHubService _gitHubService;

        public PRReviewService(
            IAIServiceFactory aIServiceFactory,
            IGitHubService gitHubService)
        {
            this.aIServiceFactory = aIServiceFactory;
            this._gitHubService = gitHubService;
        }

        public async Task<AIReviewResponse> ReviewPullRequestAsync(
            string pullRequestUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pullRequestUrl))
            {
                throw new ArgumentException("Pull request URL cannot be null or empty.", nameof(pullRequestUrl));
            }

            // ---------------------------------------------
            // 1. Get PR metadata and diff from GitHub
            // ---------------------------------------------

            var pullRequest =
                await _gitHubService.GetPullRequestAsync(
                    pullRequestUrl,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(pullRequest?.Diff))
            {
                return new AIReviewResponse();
            }

            //2. Select Gemini or OpenAI or azureOpenId based on configuration

            var aiService = aIServiceFactory.Create();

            //3. Send the PR diff to the selected AI Provider for review

            var aiResult = await aiService.ReviewCodeAsync(
                pullRequest,
                cancellationToken);

            //4. Post findings as comments to the PR on GitHub
            if(aiResult.Findings.Count>0)
            {
                await _gitHubService.AddReviewCommentsAsync(
                    pullRequest,
                    aiResult.Findings,
                    cancellationToken);
            }

            return aiResult;

        }

        // =================================================
        // Build Prompt
        // =================================================

        private static string BuildReviewPrompt(PullRequestData pullRequest)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an experienced senior software engineer");
            sb.AppendLine("performing a professional code review.");
            sb.AppendLine();
            sb.AppendLine("Pull Request:");
            sb.AppendLine($"#{pullRequest.PullRequestNumber}");
            sb.AppendLine();
            sb.AppendLine("Repository:");
            sb.AppendLine($"{pullRequest.Owner}/{pullRequest.Repository}");
            sb.AppendLine();
            sb.AppendLine("Title:");
            sb.AppendLine(pullRequest.Title);
            sb.AppendLine();
            sb.AppendLine("Description:");
            sb.AppendLine(pullRequest.Description);
            sb.AppendLine();
            sb.AppendLine("Review the following pull request diff:");
            sb.AppendLine();
            sb.AppendLine("---------------- DIFF ----------------");
            sb.AppendLine();
            sb.AppendLine(pullRequest.Diff);
            sb.AppendLine();
            sb.AppendLine("---------------- END DIFF ----------------");
            sb.AppendLine();
            sb.AppendLine("Your job is to identify meaningful issues that");
            sb.AppendLine("developers should fix.");
            sb.AppendLine();
            sb.AppendLine("Focus on:");
            sb.AppendLine();
            sb.AppendLine("- Bugs");
            sb.AppendLine("- Security problems");
            sb.AppendLine("- Performance problems");
            sb.AppendLine("- Incorrect exception handling");
            sb.AppendLine("- Incorrect async/await usage");
            sb.AppendLine("- Dependency injection problems");
            sb.AppendLine("- SOLID violations");
            sb.AppendLine("- Maintainability");
            sb.AppendLine("- Reliability");
            sb.AppendLine("- Incorrect business logic");
            sb.AppendLine("- Code smells");
            sb.AppendLine();
            sb.AppendLine("Do NOT report minor formatting issues.");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT:");
            sb.AppendLine();
            sb.AppendLine("Every finding must point to a line that exists");
            sb.AppendLine("in the pull request diff.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY valid JSON.");
            sb.AppendLine();
            sb.AppendLine("Do not use markdown.");
            sb.AppendLine("Do not use ```json.");
            sb.AppendLine("Do not add any text before or after the JSON.");
            sb.AppendLine();
            sb.AppendLine("Expected JSON format:");
            sb.AppendLine("{");
            sb.AppendLine("    \"summary\": \"Short summary of the review\",");
            sb.AppendLine("    \"findings\": [");
            sb.AppendLine("        {");
            sb.AppendLine("            \"filePath\": \"path/to/file.cs\",");
            sb.AppendLine("            \"lineNumber\": 45,");
            sb.AppendLine("            \"side\": \"RIGHT\",");
            sb.AppendLine("            \"className\": \"UserService\",");
            sb.AppendLine("            \"severity\": \"High\",");
            sb.AppendLine("            \"issue\": \"Description of the problem\",");
            sb.AppendLine("            \"recommendation\": \"What the developer should do\",");
            sb.AppendLine("            \"possibleFix\": \"Possible implementation or fix\"");
            sb.AppendLine("        }");
            sb.AppendLine("    ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine();
            sb.AppendLine("1. filePath must exactly match the file path");
            sb.AppendLine("shown in the diff.");
            sb.AppendLine();
            sb.AppendLine("2. line must be a real line number from the");
            sb.AppendLine("pull request diff.");
            sb.AppendLine();
            sb.AppendLine("3. Use RIGHT for added/context lines.");
            sb.AppendLine();
            sb.AppendLine("4. Use LEFT only for deleted lines.");
            sb.AppendLine();
            sb.AppendLine("5. If you cannot identify a valid changed line,");
            sb.AppendLine("do not create a finding.");
            sb.AppendLine();
            sb.AppendLine("6. className should contain the class name when");
            sb.AppendLine("it can be determined.");
            sb.AppendLine();
            sb.AppendLine("7. suggestedFix is optional.");
            sb.AppendLine();
            sb.AppendLine("8. Do not invent file names or line numbers.");

            return sb.ToString();
        }

        // =================================================
        // Deserialize Gemini Response
        // =================================================

        private static PRReviewResult DeserializeReview(string content)
        {
            var json = ExtractJson(content);

            var result = JsonSerializer.Deserialize<PRReviewResult>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (result is null)
            {
                throw new InvalidOperationException(
                    "AI response could not be converted to PRReviewResult.");
            }

            return result;
        }

        // =================================================
        // Gemini sometimes returns ```json ... ```
        // =================================================

        private static string ExtractJson(string content)
        {
            content = content.Trim();

            if (content.StartsWith("```"))
            {
                var firstNewLine = content.IndexOf('\n');

                var lastFence = content.LastIndexOf("```", StringComparison.Ordinal);

                if (firstNewLine >= 0 && lastFence > firstNewLine)
                {
                    content = content.Substring(firstNewLine + 1, lastFence - firstNewLine - 1);
                }
            }

            return content.Trim();
        }
    }
}