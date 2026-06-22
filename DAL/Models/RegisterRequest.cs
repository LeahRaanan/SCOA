using System.Text.Json.Serialization;

namespace SCOA.Models
{
    public class RegisterRequest
    {
        [JsonPropertyName("userName")]
        public string UserName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("chosenCategories")]
        public List<string> ChosenCategories { get; set; }

        [JsonPropertyName("preferredTimeRange")]
        public TimeRangeOption PreferredTimeRange { get; set; } = TimeRangeOption.Month;
    }
}