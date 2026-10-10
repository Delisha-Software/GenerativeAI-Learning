using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PRReviewBot.Application.Models.Gemini
{
    public class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
