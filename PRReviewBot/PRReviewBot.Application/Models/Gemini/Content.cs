using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PRReviewBot.Application.Models.Gemini
{
    public class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; } = new();
    }
}
