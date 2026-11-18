using System.ComponentModel.DataAnnotations;

namespace ANDtrSafetyMvc.Models;

public class ReviewComment
{
    public int CommentId { get; set; }

    public int ReviewNoticeId { get; set; }
    public ReviewNotice? ReviewNotice { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }

    [Required, StringLength(40)]
    public string CommentType { get; set; } = "Finding";

    [Required, StringLength(3000)]
    public string CommentText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEdited { get; set; }
}
