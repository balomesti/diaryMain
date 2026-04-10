using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using diary_api.Data;
using diary_api.Models;
using System.Security.Claims;
using System.Linq;

namespace diary_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommunityController : ControllerBase
{
    private readonly AppDbContext _context;

    public CommunityController(AppDbContext context)
    {
        _context = context;
    }

    private static readonly Dictionary<string, int> MoodScores = new()
    {
        ["terrible"] = 1, ["bad"] = 2, ["okay"] = 3, ["good"] = 4, ["amazing"] = 5
    };

    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStats([FromQuery] string period = "today")
    {
        var now = DateTime.Today;

        IQueryable<DiaryEntry> query = _context.DiaryEntries
            .Include(e => e.User)
            .Where(e => !string.IsNullOrEmpty(e.Mood));

        query = period switch
        {
            "week" => query.Where(e => e.Date >= now.AddDays(-7)),
            "month" => query.Where(e => e.Date >= now.AddDays(-30)),
            _ => query.Where(e => e.Date.Date == now)
        };

        var entryList = await query.ToListAsync();

        var moodCounts = entryList
            .GroupBy(e => e.Mood!.ToLower())
            .ToDictionary(g => g.Key, g => g.Count());

        var dominantMood = moodCounts.OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .FirstOrDefault("good");

        var avgScore = moodCounts.Count > 0
            ? moodCounts.Sum(kv => MoodScores.GetValueOrDefault(kv.Key, 3) * kv.Value)
              / (double)moodCounts.Values.Sum()
            : 3.0;

        var activeUserIds = entryList.Select(e => e.UserId).Distinct().Count();

        return Ok(new
        {
            DominantMood = dominantMood,
            TotalEntries = entryList.Count,
            ActiveUsers = activeUserIds,
            AvgCommunityScore = Math.Round(avgScore, 1),
            MoodCounts = moodCounts
        });
    }

    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string period = "today", [FromQuery] string sortBy = "score")
    {
        var now = DateTime.Today;

        IQueryable<DiaryEntry> query = _context.DiaryEntries
            .Include(e => e.User)
            .Where(e => !string.IsNullOrEmpty(e.Mood));

        query = period switch
        {
            "week" => query.Where(e => e.Date >= now.AddDays(-7)),
            "month" => query.Where(e => e.Date >= now.AddDays(-30)),
            _ => query.Where(e => e.Date.Date == now)
        };

        var entryList = await query.ToListAsync();

        var userStats = entryList
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                User = g.First().User,
                AvgMoodScore = g.Average(e => MoodScores.GetValueOrDefault(e.Mood!.ToLower(), 3)),
                CurrentMood = g.OrderByDescending(e => e.Date).First().Mood?.ToLower() ?? "good",
                EntryCount = g.Count()
            })
            .ToList();

        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(currentUserId, out var cuId);

        var avatarColors = new (string Bg, string Fg)[]
        {
            ("#EEEDFE", "#534AB7"), ("#E1F5EE", "#0F6E56"), ("#FAECE7", "#993C1D"),
            ("#FBEAF0", "#993556"), ("#E6F1FB", "#185FA5"), ("#FAEEDA", "#854F0B"),
            ("#EAF3DE", "#3B6D11"), ("#F1EFE8", "#5F5E5A"),
        };

        var result = userStats.Select((stat, index) => new
        {
            UserId = stat.UserId,
            DisplayName = stat.User?.Username ?? $"User {stat.UserId}",
            Initials = GetInitials(stat.User?.Username ?? $"User {stat.UserId}"),
            AvatarBg = avatarColors[index % avatarColors.Length].Bg,
            AvatarFg = avatarColors[index % avatarColors.Length].Fg,
            AvgMoodScore = Math.Round(stat.AvgMoodScore, 1),
            CurrentMood = stat.CurrentMood,
            Streak = CalculateUserStreak(stat.UserId),
            IsCurrentUser = stat.UserId == cuId
        }).ToList();

        if (sortBy == "streak")
            result = result.OrderByDescending(e => e.Streak).ToList();
        else
            result = result.OrderByDescending(e => e.AvgMoodScore).ToList();

        return Ok(result);
    }

    private int CalculateUserStreak(int userId)
    {
        var dates = _context.DiaryEntries
            .Where(e => e.UserId == userId)
            .Select(e => e.Date.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (dates.Count == 0) return 0;

        var streak = 0;
        var current = DateTime.Today;

        foreach (var date in dates)
        {
            if (date == current || date == current.AddDays(-1))
            {
                streak++;
                current = date;
            }
            else break;
        }

        return streak;
    }

    [HttpPost("reaction")]
    [Authorize]
    public async Task<IActionResult> ToggleReaction([FromBody] ReactionRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user" });
        }

        var validReactions = new[] { "heart", "fire", "bulb", "clap" };
        if (!validReactions.Contains(request.ReactionType.ToLower()))
        {
            return BadRequest(new { message = "Invalid reaction type" });
        }

        var existingReaction = await _context.Reactions
            .FirstOrDefaultAsync(r => r.NewsPostId == request.NewsPostId && r.UserId == userId && r.ReactionType.ToLower() == request.ReactionType.ToLower());

        if (existingReaction != null)
        {
            _context.Reactions.Remove(existingReaction);
            await _context.SaveChangesAsync();

            var counts = await _context.Reactions
                .Where(r => r.NewsPostId == request.NewsPostId)
                .GroupBy(r => r.ReactionType.ToLower())
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(new { toggled = false, counts });
        }
        else
        {
            var reaction = new Reaction
            {
                NewsPostId = request.NewsPostId,
                UserId = userId,
                ReactionType = request.ReactionType.ToLower(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Reactions.Add(reaction);
            await _context.SaveChangesAsync();

            var counts = await _context.Reactions
                .Where(r => r.NewsPostId == request.NewsPostId)
                .GroupBy(r => r.ReactionType.ToLower())
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(new { toggled = true, counts });
        }
    }

    [HttpGet("reactions/{newsPostId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReactions(int newsPostId)
    {
        var counts = await _context.Reactions
            .Where(r => r.NewsPostId == newsPostId)
            .GroupBy(r => r.ReactionType.ToLower())
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(counts);
    }

    [HttpGet("reactions/{newsPostId:int}/user")]
    [Authorize]
    public async Task<IActionResult> GetUserReactions(int newsPostId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user" });
        }

        var userReactions = await _context.Reactions
            .Where(r => r.NewsPostId == newsPostId && r.UserId == userId)
            .Select(r => r.ReactionType.ToLower())
            .ToListAsync();

        return Ok(userReactions);
    }

    [HttpPost("comment")]
    [Authorize]
    public async Task<IActionResult> AddComment([FromBody] CommentRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user" });
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "Comment text is required" });
        }

        var user = await _context.Users.FindAsync(userId);
        var username = user?.Username ?? "Anonymous";

        var comment = new Comment
        {
            NewsPostId = request.NewsPostId,
            UserId = userId,
            Text = request.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            comment.Id,
            comment.NewsPostId,
            Author = username,
            ProfileImg = user?.ProfileImg,
            comment.Text,
            CreatedAt = DateTime.SpecifyKind(comment.CreatedAt, DateTimeKind.Utc)
        });
    }

    [HttpGet("comments/{newsPostId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(int newsPostId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? currentUserId = null;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var uid))
        {
            currentUserId = uid;
        }

        var comments = await _context.Comments
            .Where(c => c.NewsPostId == newsPostId)
            .Include(c => c.User)
            .Include(c => c.Likes)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.NewsPostId,
                Author = c.User != null ? c.User.Username : "Anonymous",
                ProfileImg = c.User != null ? c.User.ProfileImg : null,
                c.Text,
                LikeCount = c.Likes.Count(),
                Liked = currentUserId.HasValue && c.Likes.Any(l => l.UserId == currentUserId.Value),
                CreatedAt = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc)
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpPost("comment/like")]
    [Authorize]
    public async Task<IActionResult> ToggleCommentLike([FromBody] CommentLikeRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user" });
        }

        var existingLike = await _context.CommentLikes
            .FirstOrDefaultAsync(l => l.CommentId == request.CommentId && l.UserId == userId);

        if (existingLike != null)
        {
            _context.CommentLikes.Remove(existingLike);
            await _context.SaveChangesAsync();

            var likeCount = await _context.CommentLikes.CountAsync(l => l.CommentId == request.CommentId);
            return Ok(new { liked = false, likeCount });
        }
        else
        {
            var like = new CommentLike
            {
                CommentId = request.CommentId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.CommentLikes.Add(like);
            await _context.SaveChangesAsync();

            var likeCount = await _context.CommentLikes.CountAsync(l => l.CommentId == request.CommentId);
            return Ok(new { liked = true, likeCount });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid user" });
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new
        {
            user.Id,
            user.Username,
            user.ProfileImg
        });
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "??";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        return parts[0].Length >= 2 ? parts[0][..2].ToUpper() : parts[0].ToUpper();
    }
}

public class ReactionRequestDto
{
    public int NewsPostId { get; set; }
    public string ReactionType { get; set; } = string.Empty;
}

public class CommentLikeRequestDto
{
    public int CommentId { get; set; }
}

public class CommentRequestDto
{
    public int NewsPostId { get; set; }
    public string Text { get; set; } = string.Empty;
}
