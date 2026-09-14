namespace PRReviewBot.Infrastructure.Configiration
{
    public class AzureOpenAIOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-10-21";
        public string DeploymentName { get; set; }

    }
}
