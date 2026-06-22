using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLL
{
    public class RecommendationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public RecommendationWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Worker התחיל לפעול ברקע...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. הגדרת אזור הזמן של ישראל
                    TimeZoneInfo israelTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
                    DateTime israelNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, israelTimeZone);

                    //  הגדרת זמן היעד הבא
                    DateTime nextRunIsrael = israelNow.Date.AddMinutes(1235);
                    if (israelNow >= nextRunIsrael)
                    {
                        nextRunIsrael = nextRunIsrael.AddDays(1);
                    }

                    //  חישוב מדויק של הזמן שנותר ל-Worker לישון
                    TimeSpan timeToWait = nextRunIsrael - israelNow;

                    Console.WriteLine($"הסבב הבא מתוזמן לשעה 10:00 בבוקר (שעון ישראל) בתאריך: {nextRunIsrael}. המתנה של {timeToWait.TotalHours:F2} שעות.");

                    // 6. ה-Worker הולך לישון עד לשעה המיועדת
                    if (timeToWait > TimeSpan.Zero)
                    {
                        await Task.Delay(timeToWait, stoppingToken);
                    }

                    // 7. הגיע הזמן! מתעוררים ומבצעים את המשימות
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var bllService = scope.ServiceProvider.GetRequiredService<BLLService>();

                        Console.WriteLine($"[{DateTime.Now}] מתחילים סבב אוטומטי של עשר בבוקר...");
                        //await bllService.GetScrapedNews();
                        await bllService.SendDailyRecommendedEmails();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"שגיאה בריצת ה-Worker: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
        }

       
    }
}