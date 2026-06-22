using SCOA.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace BLL
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://127.0.0.1:5000";
        private readonly JsonSerializerOptions _jsonOptions;

        public AIService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(40);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<List<Article>> ProcessArticlesAsync(List<Article> articles)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/process", articles, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    // קוראים את ה-JSON פעם אחת בלבד לתוך מחרוזת
                    var rawJson = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("המלצות מפייתון: " + rawJson.Substring(0, Math.Min(500, rawJson.Length)));

                    // משתמשים ב-JsonSerializer.Deserialize על המחרוזת שכבר נמצאת בזיכרון
                    return JsonSerializer.Deserialize<List<Article>>(rawJson, _jsonOptions) ?? articles;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה ב-AIService (Process): {ex.Message}");
            }
            return articles;
        }

        // פונקציה לקבלת המלצות אישיות
        public async Task<List<Article>> GetUserRecommendationsAsync(string userId, Dictionary<string, double> interestScores, List<UserArticleInteraction> allInteractions, List<Article> articles, int timeRangeDays)
        {
            try
            {
                var payload = new
                {
                    user_id = userId,
                    interest_scores = interestScores,
                    interactions = allInteractions,
                    articles,
                    time_range_days = timeRangeDays
                };

                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/get_recommendations", payload, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var rawJson = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("תשובה מ-Python (המלצות): " + rawJson.Substring(0, Math.Min(1000, rawJson.Length)));
                    return JsonSerializer.Deserialize<List<Article>>(rawJson, _jsonOptions) ?? new List<Article>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ שגיאה ב-AIService (Hybrid): {ex.Message}");
            }
            return new List<Article>();
        }


    }
}