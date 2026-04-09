using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using diary_api.Data;
using diary_api.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace diary_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public AdminController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpPost("news")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(104857600)] // 100MB
    public async Task<IActionResult> CreateNews([FromForm] CreateNewsPostDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string imageUrl = string.Empty;
        string videoUrl = string.Empty;
        var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRootPath, "uploads");

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        if (dto.Image != null)
        {
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + dto.Image.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.Image.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            imageUrl = $"{baseUrl}/uploads/{uniqueFileName}";
        }

        if (dto.Video != null)
        {
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + dto.Video.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.Video.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            videoUrl = $"{baseUrl}/uploads/{uniqueFileName}";
        }

        var author = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        var news = new NewsPost
        {
            Title = dto.Title,
            Content = dto.Content,
            ImageUrl = imageUrl,
            VideoUrl = videoUrl,
            Author = author,
            CreatedAt = dto.CreatedAt != default ? dto.CreatedAt : DateTime.UtcNow
        };

        _context.NewsPosts.Add(news);
        await _context.SaveChangesAsync();
        return Ok(news);
    }

    [HttpPost("announcements")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] Announcement announcement)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        announcement.Author = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        if (announcement.CreatedAt == default)
        {
            announcement.CreatedAt = DateTime.UtcNow;
        }
        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();
        return Ok(announcement);
    }

    [HttpGet("news")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNews()
    {
        var news = await _context.NewsPosts.OrderByDescending(n => n.CreatedAt).ToListAsync();
        return Ok(news);
    }

    [HttpGet("announcements")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAnnouncements()
    {
        var announcements = await _context.Announcements.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return Ok(announcements);
    }

    [HttpDelete("news/{id:int}")]
    public async Task<IActionResult> DeleteNews(int id)
    {
        var item = await _context.NewsPosts.FirstOrDefaultAsync(n => n.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        TryDeleteUploadByUrl(item.ImageUrl);
        TryDeleteUploadByUrl(item.VideoUrl);

        _context.NewsPosts.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("announcements/{id:int}")]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        var item = await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        _context.Announcements.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private void TryDeleteUploadByUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        var uri = TryGetUri(imageUrl);
        var path = uri?.AbsolutePath ?? imageUrl;
        if (!path.Contains("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRootPath, "uploads");
        var filePath = Path.Combine(uploadsFolder, fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private static Uri? TryGetUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        if (Uri.TryCreate("http://localhost/" + value.TrimStart('/'), UriKind.Absolute, out var relativeUri))
        {
            return relativeUri;
        }

        return null;
    }

    public sealed class CreateNewsPostDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public IFormFile? Image { get; set; }
        public IFormFile? Video { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
