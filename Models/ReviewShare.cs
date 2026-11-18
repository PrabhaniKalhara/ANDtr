namespace ANDtrSafetyMvc.Models;

public class ReviewShare
{
    public int ShareId { get; set; }

    public int ReviewId { get; set; }
    public Review? Review { get; set; }

    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser? Customer { get; set; }

    public string SharedById { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IncludeNoticeDetails { get; set; } = true;
    public bool IncludeRiskLevels { get; set; } = true;
    public bool IncludeFindings { get; set; } = true;
    public bool IncludeInternalEmployeeComments { get; set; }
    public bool IncludeInternalCompanyNotes { get; set; }
    public bool IncludeRecommendations { get; set; } = true;
}
