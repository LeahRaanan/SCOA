using MongoDB.Driver;
using SCOA.Models;

namespace SCOA.DAL
{
    public class MongoDBService
    {
        private readonly IMongoDatabase _database;

        public MongoDBService(string connectionString, string dbName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(dbName);
        }

        // --- Users ---
        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public void AddUser(User user)
        {
            Users.InsertOne(user);
        }

        public User GetUserById(string id) =>
            Users.Find(u => u.Id == id).FirstOrDefault();

        public User? GetUserByUsername(string username) =>
            Users.Find(u => u.UserName == username).FirstOrDefault();


        public User? GetUserByEmailHash(string emailHash)
        {
            return Users.Find(u => u.EmailHash == emailHash).FirstOrDefault();
        }

        public void UpdateUser(User user) =>
            Users.ReplaceOne(u => u.Id == user.Id, user);

        // --- Articles ---
        public IMongoCollection<Article> Articles => _database.GetCollection<Article>("Articles");

        public void AddArticle(Article article)
        {
            var existing = Articles.Find(a => a.SourceUrl == article.SourceUrl).FirstOrDefault();
            if (existing == null)
                Articles.InsertOne(article);
        }

        public Article GetArticleById(string id) =>
            Articles.Find(a => a.Id == id).FirstOrDefault();

        public List<Article> GetAllArticles() =>
            Articles.Find(_ => true).ToList();

        // --- Interactions ---
        public IMongoCollection<UserArticleInteraction> UserInteractions =>
            _database.GetCollection<UserArticleInteraction>("UserInteractions");

        public void SaveInteraction(UserArticleInteraction interaction) =>
            UserInteractions.InsertOne(interaction);
    }
}