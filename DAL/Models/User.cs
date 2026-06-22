using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson;

namespace SCOA.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string UserName { get; set; }
        public string Password { get; set; }
        public string EmailHash { get; set; }
        public string Email { get; set; }

        [BsonRepresentation(BsonType.String)]
        public List<string> ChosenCategories { get; set; }

        // טווח הזמן שהמשתמש בחר לבדיקת היסטוריית הקריאה שלו (משפיע על מנוע ההמלצות)
        [BsonRepresentation(BsonType.String)]
        [BsonDefaultValue(TimeRangeOption.Month)]
        public TimeRangeOption PreferredTimeRange { get; set; }

        public Dictionary<string, double> InterestScores { get; set; }

        public DateTime CreatedAt { get; set; }

        public User()
        {
            ChosenCategories = new List<string>();
            InterestScores = new Dictionary<string, double>();
            PreferredTimeRange = TimeRangeOption.Month;
            foreach (string cat in Category.AllCat)
            {
                InterestScores[cat.ToString()] = 0.0;
            }
            CreatedAt = DateTime.UtcNow;
        }
    }
}