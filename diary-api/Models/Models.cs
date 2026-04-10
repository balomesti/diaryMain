using System.ComponentModel.DataAnnotations;

namespace diary_api.Models;

public class User
{
    public int Id { get; set; }
    [Required]
    public string Username { get; set; } = string.Empty;
    [Required]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public string? ProfileImg { get; set; }
    public string? UserBio { get; set; }
}

public class DiaryEntry
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public string ImageUrls { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User? User { get; set; }
}

public class NewsPost
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Content { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string Author { get; set; } = "Admin";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Announcement
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = "Admin";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StreakInfo
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime? LastEntryDate { get; set; }
    public bool HasEntryToday { get; set; }
}

public class Reaction
{
    public int Id { get; set; }
    public int NewsPostId { get; set; }
    public int UserId { get; set; }
    public string ReactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public NewsPost? NewsPost { get; set; }
    public User? User { get; set; }
}

public class Comment
{
    public int Id { get; set; }
    public int NewsPostId { get; set; }
    public int UserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public NewsPost? NewsPost { get; set; }
    public User? User { get; set; }
}
