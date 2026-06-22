using HtmlAgilityPack;
using MongoDB.Driver;
using SCOA.DAL;
using SCOA.Models;
using SCOA.Services;
using System.Net.Http.Json;

namespace BLL
{
    public class BLLService
    {
        private readonly MongoDBService _dal;
        private readonly AIService _aiService;
        private readonly HttpClient _httpClient;
        private readonly IEmailService _emailSender;
        private readonly SecurityService _security;

        private readonly string url = "https://www.theyeshivaworld.com/news/category/headlines-breaking-stories,promotions";

        public BLLService(HttpClient httpClient, MongoDBService dal, AIService aiService, IEmailService emailSender, SecurityService security)
        {
            _dal = dal;
            _httpClient = httpClient;
            _aiService = aiService;
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            _emailSender = emailSender;
            _security = security;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ניהול משתמשים - חישובים והצפנות מבוצעים כאן ב-BLL
        // ════════════════════════════════════════════════════════════════════

        public User? GetUserByEmail(string email)
        {
            // מייצרים האש מהמייל הנקי ומבקשים מה-DAL לחפש לפיו
            string emailHash = _security.HashEmail(email);
            return _dal.GetUserByEmailHash(emailHash);
        }

        public User? GetUserById(string id)
        {
            return _dal.GetUserById(id);
        }

        public string RegisterUser(RegisterRequest request)
        {
            // ביצוע כל ההצפנות וההאשים כאן בתוך שכבת הלוגיקה
            var user = new User
            {
                UserName = request.UserName,
                EmailHash = _security.HashEmail(request.Email),  // SHA-256 לחיפוש מהיר
                Email = _security.EncryptEmail(request.Email),  // AES להצפנת המידע
                Password = _security.HashPassword(request.Password), // האש לסיסמה
                ChosenCategories = request.ChosenCategories ?? new List<string>(),
                PreferredTimeRange = request.PreferredTimeRange,
                InterestScores = new Dictionary<string, double>()
            };

            // אתחול ציוני עניין ראשוניים
            foreach (var category in user.ChosenCategories)
            {
                user.InterestScores[category] = 0.5;
            }

            _dal.AddUser(user);
            return user.Id;
        }

        public void AddUser(User user)
        {
            user.EmailHash = _security.HashEmail(user.Email);
            user.Email = _security.EncryptEmail(user.Email);
            user.Password = _security.HashPassword(user.Password);

            _dal.AddUser(user);
        }

        // ════════════════════════════════════════════════════════════════════
        //  המלצות ואינטראקציות
        // ════════════════════════════════════════════════════════════════════

        public async Task<List<Article>> GetRecommendationsForUser(string userId)
        {
            var user = _dal.GetUserById(userId);
            if (user == null) return new List<Article>();

            int windowDays = user.PreferredTimeRange.ToDays();
            var historyWindowStart = DateTime.UtcNow.Date.AddDays(-windowDays);

            var recentInteractions = _dal.UserInteractions
                .Find(i => i.ClickedAt >= historyWindowStart)
                .ToList();

            var oneWeekAgo = DateTime.UtcNow.Date.AddDays(-7);
            var recentArticles = _dal.GetAllArticles()
                .Where(a => a.PublishedDate >= oneWeekAgo)
                .ToList();

            return await _aiService.GetUserRecommendationsAsync(
                user.Id,
                user.InterestScores,
                recentInteractions,
                recentArticles,
                windowDays
            );
        }

        public async Task HandleUserClick(string userId, string articleId)
        {
            var interaction = new UserArticleInteraction
            {
                UserId = userId,
                ArticleId = articleId,
                ClickedAt = DateTime.UtcNow
            };
            _dal.SaveInteraction(interaction);

            var article = _dal.GetArticleById(articleId);
            var user = _dal.GetUserById(userId);

            if (article != null && user != null)
            {
                user.InterestScores ??= new Dictionary<string, double>();

                if (article.Categories != null)
                {
                    foreach (var cat in article.Categories)
                    {
                        if (!user.InterestScores.ContainsKey(cat))
                        {
                            user.InterestScores[cat] = 0.0;
                        }

                        user.InterestScores[cat] += 0.5;
                        if (user.InterestScores[cat] > 1.0)
                        {
                            user.InterestScores[cat] = 1.0;
                        }
                    }
                }
                _dal.UpdateUser(user);
            }
        }


        public async Task<string> HandleMailClick(string userHash, string articleId)
        {

            var user = _dal.GetUserByEmailHash(userHash);
            if (user == null) return null;

            var article = _dal.GetArticleById(articleId);
            if (article == null) return null;

            await HandleUserClick(user.Id, articleId);

            return article.SourceUrl;
        }


        public async Task PenalizeUnreadArticles(string userId, List<string> sentArticleIds)
        {
            var user = _dal.GetUserById(userId);
            if (user == null) return;

            var clickedArticleIds = _dal.UserInteractions
                .Find(i => i.UserId == userId)
                .ToList()
                .Select(i => i.ArticleId)
                .ToHashSet();

            var unreadIds = sentArticleIds.Where(id => !clickedArticleIds.Contains(id)).ToList();

            foreach (var articleId in unreadIds)
            {
                var article = _dal.GetArticleById(articleId);
                if (article == null) continue;

                foreach (var cat in article.Categories)
                {
                    if (user.InterestScores.ContainsKey(cat))
                    {
                        user.InterestScores[cat] -= 0.1;
                        if (user.InterestScores[cat] < 0) user.InterestScores[cat] = 0;
                    }
                }
            }
            _dal.UpdateUser(user);
        }

        // ════════════════════════════════════════════════════════════════════
        //  שליחת מיילים
        // ════════════════════════════════════════════════════════════════════

        public async Task SendDailyRecommendedEmails()
        {
            var allUsers = _dal.Users.Find(_ => true).ToList();
            if (!allUsers.Any()) return;

            foreach (var user in allUsers)
            {
                try
                {
                    string plainEmail = _security.DecryptEmail(user.Email);
                    var pythonRecommendations = await GetRecommendationsForUser(user.Id);

                    if (pythonRecommendations == null || !pythonRecommendations.Any())
                    {
                        Console.WriteLine($"[BLL] אין המלצות מפייתון עבור {plainEmail}. מדלג...");
                        continue;
                    }

                    // 🛑 התיקון: יצירת תאריך אתמול מאופס כ-UTC במפורש
                    var yesterdayLocal = DateTime.Today.AddDays(-1);
                    var yesterdayUtc = DateTime.SpecifyKind(yesterdayLocal, DateTimeKind.Utc);

                    var recommendedUrls = pythonRecommendations.Select(a => a.SourceUrl).ToList();

                    // שליפה מה-DB בהשוואה מדויקת של UTC ל-UTC
                    var articlesToSend = _dal.Articles
                        .Find(article => recommendedUrls.Contains(article.SourceUrl) && article.PublishedDate == yesterdayUtc)
                        .ToList();

                    // אם אין כתבות מאתמול, אין טעם לשלוח מייל ריק
                    if (!articlesToSend.Any())
                    {
                        Console.WriteLine($"[BLL] לא נמצאו כתבות ב-DB התואמות להמלצות עבור התאריך {yesterdayUtc:yyyy-MM-dd} עבור {plainEmail}");
                        continue;
                    }

                    await _emailSender.SendRecommendationEmailAsync(user.EmailHash, plainEmail, user.InterestScores, articlesToSend);
                    Console.WriteLine($"[BLL] המייל נשלח בהצלחה לתיבה של: {plainEmail} (נשלחו {articlesToSend.Count} כתבות)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BLL] שגיאה בעיבוד המשתמש {user.Id}: {ex.Message}");
                }
            }
        }



        // ════════════════════════════════════════════════════════════════════
        //  סקרייפינג (Scraping) ועיבוד נתונים
        // ════════════════════════════════════════════════════════════════════

        public async Task<List<Article>> GetScrapedNews()
        {
            Console.WriteLine("\n--- [התחלת תהליך משיכת נתונים] ---");
            var allRawArticles = new List<Article>();
            int page = 1;
            int maxPages = 4;

            while (page <= maxPages)
            {
                string pageUrl = page == 1 ? url : $"{url}/page/{page}";
                string html = await CallUrl(pageUrl);

                if (string.IsNullOrEmpty(html)) break;

                var (articles, hasYesterday) = await ParseHtml(html);

                if (articles != null && articles.Any())
                {

                    allRawArticles.AddRange(articles);
                }

                page++;
            }

            Console.WriteLine($"--- [סיום סריקה. נמצאו {allRawArticles.Count} כתבות לעיבוד] ---");

            var processedArticles = await _aiService.ProcessArticlesAsync(allRawArticles);

            if (processedArticles != null)
            {
                foreach (var article in processedArticles)
                {
                    Console.WriteLine($"שומר: {article.Title} | categories: {string.Join(", ", article.Categories ?? new List<string>())}");
                    article.Id = null;
                    _dal.AddArticle(article);
                }
            }

            return processedArticles ?? new List<Article>();
        }

        private async Task<string> CallUrl(string fullUrl)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                var response = await _httpClient.SendAsync(request);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ❌ שגיאה ב-CallUrl: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<(List<Article> articles, bool hasYesterday)> ParseHtml(string html)
        {
            var articlesData = new List<Article>();
            bool hasYesterday = false;
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var articleContainers = doc.DocumentNode.SelectNodes("//div[contains(@class, 'e-loop-item')]");

            if (articleContainers != null)
            {

                var yesterdayUtc = DateTime.SpecifyKind(DateTime.Today.AddDays(-1), DateTimeKind.Utc);
                var todayUtc = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

                foreach (var container in articleContainers)
                {
                    if (container.SelectSingleNode(".//*[contains(@class, 'ccstyle')]") != null) continue;

                    var linkNode = container.SelectSingleNode(".//h4[contains(@class, 'elementor-heading-title')]//a");
                    if (linkNode == null) continue;

                    var timeNode = container.SelectSingleNode(".//time");

                    if (timeNode == null || !DateTime.TryParse(timeNode.InnerText.Trim(),
                         System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.AssumeUniversal, // אומר לקוד להתייחס לטקסט כתאריך גלובלי
                         out DateTime parsedDate))
                    {
                        continue;
                    }
                    DateTime publishedDate = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Utc);

                    if (publishedDate < yesterdayUtc)
                    {
                        break;
                    }

                    if (publishedDate >= todayUtc)
                    {
                        continue;
                    }

                    hasYesterday = true;

                    string title = System.Net.WebUtility.HtmlDecode(linkNode.InnerText.Trim());
                    string linkUrl = linkNode.GetAttributeValue("href", "");

                    if (articlesData.Any(a => a.SourceUrl == linkUrl)) continue;

                    await Task.Delay(500);
                    try
                    {
                        string rawContent = await GetArticleContentAsync(linkNode);
                        if (rawContent == "No relevant content found") continue;

                        string cleanContent = System.Net.WebUtility.HtmlDecode(rawContent);
                        int UrgencyLevel = 0;

                        if (title.Contains("🚨🚨"))
                        {
                            UrgencyLevel = 2; // קריטי ביותר
                        }
                        else if (title.Contains("🚨"))
                        {
                            UrgencyLevel = 1; // דחוף
                        }

                        articlesData.Add(new Article
                        {
                            Title = title,
                            SourceUrl = linkUrl,
                            FullText = cleanContent,
                            PublishedDate = publishedDate,
                            UrgencyLevel = UrgencyLevel
                        });
                    }
                    catch { }
                }
            }

            return (articlesData, hasYesterday);
        }

        public async Task<string> GetArticleContentAsync(HtmlNode a)
        {
            string articleUrl = a.GetAttributeValue("href", "");
            var response = await _httpClient.GetStringAsync(articleUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var bodyNode = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class, 'entry-content')] | //div[contains(@class, 'elementor-widget-theme-post-content')]"
            );

            if (bodyNode != null)
            {
                var paragraphs = bodyNode.SelectNodes(".//p");
                if (paragraphs != null)
                {
                    return string.Join("\n\n", paragraphs.Select(p => p.InnerText.Trim()).Where(t => t.Length > 10));
                }
            }
            return "No relevant content found";
        }

        public List<Article> GetRecentArticles()
        {
            var oneWeekAgo = DateTime.UtcNow.Date.AddDays(-7);
            return _dal.GetAllArticles()
                .Where(a => a.PublishedDate >= oneWeekAgo)
                .OrderByDescending(a => a.PublishedDate)
                .ToList();
        }

        public void UpdateUserProfile(string userId, string newName, List<string> newCategories, TimeRangeOption preferredTimeRange)
        {
            var existingUser = _dal.GetUserById(userId);
            if (existingUser == null) return;

            existingUser.UserName = newName;
            existingUser.ChosenCategories = newCategories;
            existingUser.PreferredTimeRange = preferredTimeRange;

            foreach (var category in existingUser.InterestScores.Keys.ToList())
            {
                bool isNowSelected = newCategories.Contains(category);

                if (isNowSelected)
                {
                    if (existingUser.InterestScores[category] < 0.0)
                    {
                        existingUser.InterestScores[category] = 0.5;
                    }
                }
                else
                {
                    existingUser.InterestScores[category] = 0.0;
                }
            }

            _dal.UpdateUser(existingUser);
        }
    }
}