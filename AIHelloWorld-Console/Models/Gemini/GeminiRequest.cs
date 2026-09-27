using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace AIHelloWorld.Console.Models.Gemini
{
    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; } = new();
    }
}
