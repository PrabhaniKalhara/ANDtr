using System.ComponentModel.DataAnnotations;

namespace ANDtrSafetyMvc.Models;

public class Review
{
    public int ReviewId { get; set; }

    [Required]
    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "In progress";

    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReviewNotice> Items { get; set; } = new List<ReviewNotice>();
    public ICollection<ReviewShare> Shares { get; set; } = new List<ReviewShare>();
    public ICollection<ReportRecord> Reports { get; set; } = new List<ReportRecord>();
}
