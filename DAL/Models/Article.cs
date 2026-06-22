using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SCOA.Models;
using System.Text.Json.Serialization;

namespace SCOA.Models
{
    [BsonIgnoreExtraElements]
    public class Article
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [JsonPropertyName("title")] 
        public string Title { get; set; }

        [JsonPropertyName("fullText")]
        public string FullText { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }

        [JsonPropertyName("sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonPropertyName("publishedDate")]
        public DateTime PublishedDate { get; set; }

        [JsonPropertyName("readingTime")]
        public int ReadingTime => string.IsNullOrEmpty(FullText) ? 0
    : (int)Math.Ceiling(FullText.Split(' ').Length / 200.0);

        [JsonPropertyName("isUrgent")]
        public int UrgencyLevel { get; set; }
        public Article()
        {
            Categories = new List<string>();
        }
    }



}
