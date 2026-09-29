using System.ComponentModel.DataAnnotations;

namespace JobAppTrackerApi.DTOs
{
    public class CreateRequest
    {
        [Required, MinLength(1), MaxLength(100)]
        public string Title { get; set; } = string.Empty;
        [Required, MinLength(1), MaxLength(100)]
        public string Company { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        [Url]
        public string? Link { get; set; }
        public DateOnly? AppliedDate { get; set; }
    }

    public class UpdateRequest
    {
        [MinLength(1), MaxLength(200)]
        public string? Title { get; set; }

        [MinLength(1), MaxLength(200)]
        public string? Company { get; set; }

        [MinLength(1), MaxLength(2000)]
        public string? Description { get; set; }

        [Url]
        public string? Link { get; set; }

        public DateOnly? AppliedDate { get; set; }
    }

    public record JobAppResponse(
        int Id,
        string Title,
        string Company,
        string Description,
        string Link,
        string Status,
        DateOnly AppliedDate,
        DateOnly UpdatedDate
    );
}
