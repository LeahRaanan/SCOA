namespace SCOA.Models
{
    public static class Category
    {
        public const string ArtsCultureAndEntertainment = "Arts, Culture, and Entertainment";
        public const string BusinessAndFinance = "Business and Finance";
        public const string Crime = "Crime";
        public const string HealthAndWellness = "Health and Wellness";
        public const string LifestyleAndFashion = "Lifestyle and Fashion";
        public const string Politics = "Politics";
        public const string ScienceAndTechnology = "Science and Technology";
        public const string Sports = "Sports";

        public static readonly List<string> AllCat = new List<string>
        {
            ArtsCultureAndEntertainment,
            BusinessAndFinance,
            Crime,
            HealthAndWellness,
            LifestyleAndFashion,
            Politics,
            ScienceAndTechnology,
            Sports
        };
    }
}