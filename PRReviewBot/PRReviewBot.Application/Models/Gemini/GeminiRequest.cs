using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PRReviewBot.Application.Models.Gemini
{
    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; } = new();
    }
}
