using System.Text.Json.Serialization;

namespace SCOA.Models
{
    // טווח הזמן שממנו המערכת בודקת את היסטוריית הקריאה של המשתמש
    // לצורך בניית פרופיל ההעדפות וההמלצות.
    // הערכים תואמים בדיוק למחרוזות שנשלחות מ-Angular (TimeRange ב-register.ts / profile.ts):
    // 'Week' | 'TwoWeeks' | 'Month' | 'SixMonths'
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TimeRangeOption
    {
        Week,
        TwoWeeks,
        Month,
        SixMonths
    }

    public static class TimeRangeOptionExtensions
    {
        // ממיר את הטווח הנבחר למספר ימים בפועל - משמש לסינון מאמרים/אינטראקציות לפי תאריך
        public static int ToDays(this TimeRangeOption range) => range switch
        {
            TimeRangeOption.Week => 7,
            TimeRangeOption.TwoWeeks => 14,
            TimeRangeOption.Month => 30,
            TimeRangeOption.SixMonths => 182,
            _ => 30
        };
    }
}
