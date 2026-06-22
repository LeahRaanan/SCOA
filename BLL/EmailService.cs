using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using MailKit.Net.Smtp;
using MimeKit;
using SCOA.Models;

namespace BLL
{
    public interface IEmailService
    {
        Task SendRecommendationEmailAsync(string userHash, string toEmail, Dictionary<string, double> categoryWeights, List<Article> articles);
    }

    public class EmailService : IEmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _port = 587;
        private readonly string _senderEmail = "scoanewssystem@gmail.com";
        private readonly string _appPassword = "ires tira wyki zmor";

        public async Task SendRecommendationEmailAsync(string userHash, string toEmail, Dictionary<string, double> categoryWeights, List<Article> articles)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("SCOA News", _senderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "Your Recommended Articles for Today";

            var bodyBuilder = new BodyBuilder();

            string htmlContent = @"
    <div style='background-color: #f7f7f5; padding: 40px 20px; font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; direction: rtl; text-align: right;'>
        <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);'>
            <h1 style='color: #5A5A40; font-size: 24px; font-weight: 400; margin-top: 0; margin-bottom: 10px; border-bottom: 1px solid #e5e5e0; padding-bottom: 15px;'>Your Reading Recommendations</h1>
            <p style='color: #707065; font-size: 14px; margin-bottom: 30px; line-height: 1.5;'>We have curated the latest articles personally selected to match your interests.</p>";

            List<Article> displayNews = new List<Article>();
            List<Article> extraNews = new List<Article>();

            if (categoryWeights != null && categoryWeights.Any())
            {
                // 1. כתבות שמתאימות לפחות לקטגוריה אחת של המשתמש
                displayNews = articles
                    .Where(a => a.Categories != null && a.Categories.Any(cat => categoryWeights.TryGetValue(cat, out double weight) && weight > 0))
                    // שלב א': הקפצת דחופות (כיוון שזה int, כתבות ישנות יהיו אוטומטית 0)
                    .OrderByDescending(a => a.UrgencyLevel)
                    // שלב ב': מיון לפי הציון המקסימלי מבין הקטגוריות של הכתבה
                    .ThenByDescending(a => a.Categories != null && a.Categories.Any()
                        ? a.Categories.Max(cat => categoryWeights.TryGetValue(cat, out double w) ? w : 0.0)
                        : 0.0)
                    // שלב ג': שובר שוויון לפי סדר אלף-ביתי של הכותרת
                    .ThenBy(a => a.Title ?? string.Empty)
                    .ToList();

                // 2. כתבות לגיוון (לא מתאימות לאף קטגוריה נבחרת)
                extraNews = articles
                    .Where(a => a.Categories == null || !a.Categories.Any(cat => categoryWeights.TryGetValue(cat, out double weight) && weight > 0))
                    // גם בגיוון: קודם כל דחיפות, ואז שובר שוויון בא"ב
                    .OrderByDescending(a => a.UrgencyLevel)
                    .ThenBy(a => a.Title ?? string.Empty)
                    .ToList();
            }
            else
            {
                // אם אין למשתמש משקלים בכלל, נציג את הכל ב-displayNews לפי דחיפות ואז א"ב
                displayNews = articles
                    .OrderByDescending(a => a.UrgencyLevel)
                    .ThenBy(a => a.Title ?? string.Empty)
                    .ToList();
            }

            foreach (var article in displayNews)
            {
                htmlContent += BuildArticleMarkup(article, userHash, isExtra: false);
            }

            if (extraNews.Any())
            {
                htmlContent += @"
        <div style='margin: 40px 0 25px 0; text-align: center;'>
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                <tr>
                    <td style='border-bottom: 1px solid #e5e5e0; vertical-align: middle;'></td>
                    <td style='width: 1%; white-space: nowrap; padding: 0 15px; font-family: Georgia, serif; font-size: 16px; color: #1a1a1a; font-style: italic;'>
                        You might love this too
                    </td>
                    <td style='border-bottom: 1px solid #e5e5e0; vertical-align: middle;'></td>
                </tr>
            </table>
        </div>";

                foreach (var article in extraNews)
                {
                    htmlContent += BuildArticleMarkup(article, userHash, isExtra: true);
                }
            }

            htmlContent += @"
            <div style='margin-top: 40px; text-align: center; border-top: 1px solid #e5e5e0; padding-top: 20px;'>
                <p style='color: #a0a095; font-size: 11px; margin: 0;'>Automatically sent by SCOA News System</p>
            </div>
        </div>
    </div>";

            bodyBuilder.HtmlBody = htmlContent;
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_smtpServer, _port, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_senderEmail, _appPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }

        private string BuildArticleMarkup(Article article, string userHash, bool isExtra)
        {
            string tagsMarkup = "<div style='margin-bottom: 12px;'>";

            // כאן הוסר ה-?? 0 מכיוון ש-UrgencyLevel הוא int
            if (article.UrgencyLevel == 2)
            {
                tagsMarkup += "<span style='background-color: #fee2e2; color: #b91c1c; border: 1px solid #fca5a5; font-size: 11px; font-weight: bold; padding: 3px 8px; border-radius: 12px; margin-left: 6px; display: inline-block; letter-spacing: 0.5px;'>Breaking News</span>";
            }
            else if (article.UrgencyLevel == 1)
            {
                tagsMarkup += "<span style='background-color: #fef3c7; color: #b45309; border: 1px solid #fde68a; font-size: 11px; font-weight: bold; padding: 3px 8px; border-radius: 12px; margin-left: 6px; display: inline-block; letter-spacing: 0.5px;'>Breaking News</span>";
            }

            if (isExtra)
            {
                string mainCategory = article.Categories != null && article.Categories.Any()
                    ? article.Categories.First()
                    : "General";

                tagsMarkup += $"<span style='background-color: #f3e8ff; color: #6b21a8; border: 1px solid #e9d5ff; font-size: 11px; font-weight: bold; padding: 3px 8px; border-radius: 12px; margin-left: 6px; display: inline-block; text-transform: uppercase; letter-spacing: 0.5px;'>{mainCategory}</span>";
            }
            else
            {
                if (article.Categories != null && article.Categories.Any())
                {
                    foreach (var category in article.Categories)
                    {
                        tagsMarkup += $"<span style='background-color: #f0f0eb; color: #5A5A40; font-size: 11px; font-weight: 600; padding: 3px 8px; border-radius: 2px; margin-left: 6px; display: inline-block; text-transform: uppercase; letter-spacing: 0.5px;'>{category}</span>";
                    }
                }
                else
                {
                    tagsMarkup += "<span style='background-color: #f9fafb; color: #4b5563; border: 1px solid #e5e7eb; font-size: 11px; font-weight: 600; padding: 3px 8px; border-radius: 12px; margin-left: 6px; display: inline-block; text-transform: uppercase; letter-spacing: 0.5px;'>General</span>";
                }
            }

            tagsMarkup += "</div>";

            string description = !string.IsNullOrEmpty(article.Summary) ? article.Summary :
                                 (!string.IsNullOrEmpty(article.FullText) && article.FullText.Length > 150
                                    ? article.FullText.Substring(0, 150) + "..."
                                    : article.FullText);

            string clickUrl = $"https://localhost:7271/api/News/click_from_mail?userHash={userHash}&articleId={article.Id}";

            return $@"
        <div style='margin-bottom: 35px; border-bottom: 1px solid #f0f0eb; padding-bottom: 25px;'>
            {tagsMarkup}
            <h2 style='color: #2b2b25; font-size: 19px; font-weight: 500; margin: 0 0 6px 0; line-height: 1.4;'>{article.Title}</h2>
            <div style='color: #8c8c80; font-size: 12px; margin-bottom: 14px;'>
               Estimated reading time: {article.ReadingTime} min
            </div>
            <p style='color: #4a4a40; font-size: 14px; line-height: 1.6; margin: 0 0 16px 0; border-right: 2px solid #5A5A40; padding-right: 12px;'>
                {description}
            </p>
            <div>
                <a href='{clickUrl}' style='color: #5A5A40; text-decoration: none; font-size: 13px; font-weight: 600; border-bottom: 1px solid #5A5A40; padding-bottom: 2px; display: inline-block;'>Read full article</a>
            </div>
        </div>";
        }
    }
}