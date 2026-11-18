using System.ComponentModel.DataAnnotations;

namespace ANDtrSafetyMvc.Models;

public class Notice
{
    public int NoticeId { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [StringLength(100)]
    public string DeviceName { get; set; } = string.Empty;

    [StringLength(120)]
    public string Manufacturer { get; set; } = string.Empty;

    [StringLength(80)]
    public string DeviceType { get; set; } = string.Empty;

    [StringLength(100)]
    public string ProblemType { get; set; } = string.Empty;

    [StringLength(40)]
    public string SeverityClass { get; set; } = "Class II";

    public DateOnly DatePublished { get; set; }

    [StringLength(80)]
    public string NoticeNumber { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(12000)]
    public string SafetyInformation { get; set; } = string.Empty;

    [StringLength(12000)]
    public string RecommendedAction { get; set; } = string.Empty;

    public ICollection<ReviewNotice> ReviewNotices { get; set; } = new List<ReviewNotice>();
}
