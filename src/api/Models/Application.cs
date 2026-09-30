namespace JobAppTrackerApi.Models
{
    public enum AppStatus
    {
        Applied,
        Interview,
        Rejected,
        Archived
    }

    public class Application
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public AppStatus Status { get; set; } = AppStatus.Applied;

        public DateOnly AppliedDate { get; set; }
        public DateOnly UpdatedDate { get; set; }

        public string? Description { get; set; }
        public string? Link { get; set; }
    }
}
