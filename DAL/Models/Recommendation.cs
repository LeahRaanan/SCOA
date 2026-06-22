using System.Text.Json.Serialization;

namespace SCOA.Models
{
    public class Recommendation
    {
            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("score")]
            public double Score { get; set; }
    }
}
