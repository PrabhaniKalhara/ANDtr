using System.ComponentModel.DataAnnotations;

namespace ANDtrSafetyMvc.Models;

public class ReviewNotice
{
    public int ReviewNoticeId { get; set; }

    public int ReviewId { get; set; }
    public Review? Review { get; set; }

    public int NoticeId { get; set; }
    public Notice? Notice { get; set; }

    [Required, StringLength(20)]
    public string RiskLevel { get; set; } = "Not rated";

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ReviewComment> Comments { get; set; } = new List<ReviewComment>();
}
