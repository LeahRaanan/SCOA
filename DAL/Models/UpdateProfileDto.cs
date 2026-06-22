namespace SCOA.Models
{
    public class UpdateProfileDto
    {
        public string UserName { get; set; }
        public List<string> ChosenCategories { get; set; }
        public TimeRangeOption PreferredTimeRange { get; set; } = TimeRangeOption.Month;
    }
}