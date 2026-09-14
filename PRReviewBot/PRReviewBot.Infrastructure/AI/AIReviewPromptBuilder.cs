using PRReviewBot.Application.Models.Common;
using System.Text;

namespace PRReviewBot.Infrastructure.AI
{
    public static class AIReviewPromptBuilder
    {
        public static string BuildReviewPrompt(PullRequestData pullRequest)
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
    }
}