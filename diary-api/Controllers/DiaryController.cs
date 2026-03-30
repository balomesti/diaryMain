using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using diary_api.Data;
using diary_api.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace diary_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Protect all endpoints in this controller
public class DiaryController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public DiaryController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    /// POST api/Diary ( Creates a new diary entry for the current authenticated user.)
    /// Form fields: Title (string), Content (string), Date (date/datetime), Image (file, optional).
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateEntry([FromForm] DiaryEntryDto entryDto)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var entry = new DiaryEntry
            {
                Title = entryDto.Title,
                Content = entryDto.Content,
                Date = entryDto.Date,
                Location = entryDto.Location,
                Weather = entryDto.Weather,
                Mood = entryDto.Mood,
                Tags = entryDto.Tags,
                UserId = int.Parse(userId)
            };

            if (entryDto.Images != null && entryDto.Images.Count > 0)
            {
                var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploadsFolder = Path.Combine(webRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var uploadedUrls = new List<string>();
                foreach (var file in entryDto.Images)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    uploadedUrls.Add("/uploads/" + uniqueFileName);
                }
                entry.ImageUrls = string.Join(";", uploadedUrls);
            }

            _context.DiaryEntries.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(entry);
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error");
        }
    }

    /// GET api/Diary (Returns all diary entries for the current authenticated user)
    [HttpGet]
    public async Task<IActionResult> GetEntries()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var entries = await _context.DiaryEntries
            .Where(e => e.UserId == int.Parse(userId))
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        return Ok(entries);
    }

    /// GET api/Diary/{id} (Returns a single diary entry for the current authenticated user)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEntry(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var entry = await _context.DiaryEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == int.Parse(userId));

        if (entry == null) return NotFound();

        return Ok(entry);
    }

    /// DELETE api/Diary/{id} (Deletes a diary entry)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var entry = await _context.DiaryEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == int.Parse(userId));
        if (entry == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(entry.ImageUrls))
        {
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var urls = entry.ImageUrls.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in urls)
            {
                if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var filePath = Path.Combine(webRootPath, relativePath);

                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }
        }

        _context.DiaryEntries.Remove(entry);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// PUT api/Diary/{id} (Updates an existing diary entry)
    /// Accepts the same multipart fields as POST. If Image is supplied, replaces the old image.
    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateEntry(int id, [FromForm] DiaryEntryDto entryDto)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var entry = await _context.DiaryEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == int.Parse(userId));
            if (entry == null) return NotFound();

            entry.Title = entryDto.Title;
            entry.Content = entryDto.Content;
            entry.Date = entryDto.Date;
            entry.Location = entryDto.Location;
            entry.Weather = entryDto.Weather;
            entry.Mood = entryDto.Mood;
            entry.Tags = entryDto.Tags;

            if (entryDto.Images != null && entryDto.Images.Count > 0)
            {
                var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                // Optional: Delete old images if replacing
                if (!string.IsNullOrWhiteSpace(entry.ImageUrls))
                {
                    var oldUrls = entry.ImageUrls.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var oldUrl in oldUrls)
                    {
                        if (oldUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                        {
                            var relativePathOld = oldUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                            var filePathOld = Path.Combine(webRootPath, relativePathOld);
                            if (System.IO.File.Exists(filePathOld))
                            {
                                System.IO.File.Delete(filePathOld);
                            }
                        }
                    }
                }

                var uploadsFolder = Path.Combine(webRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uploadedUrls = new List<string>();
                foreach (var file in entryDto.Images)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    uploadedUrls.Add("/uploads/" + uniqueFileName);
                }
                entry.ImageUrls = string.Join(";", uploadedUrls);
            }

            await _context.SaveChangesAsync();
            return Ok(entry);
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("streak")]
    public async Task<IActionResult> GetStreak()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var entries = await _context.DiaryEntries
            .Where(e => e.UserId == int.Parse(userId))
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        if (entries == null || !entries.Any())
        {
            return Ok(new StreakInfo
            {
                CurrentStreak = 0,
                LongestStreak = 0,
                LastEntryDate = null,
                HasEntryToday = false
            });
        }

        var distinctDates = entries
            .Select(e => e.Date.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        var today = DateTime.Today;
        var hasEntryToday = distinctDates.Any(d => d == today);
        var lastEntryDate = distinctDates.FirstOrDefault();

        int currentStreak = 0;
        int longestStreak = 0;
        int tempStreak = 0;
        DateTime? previousDate = null;

        foreach (var date in distinctDates.OrderBy(d => d))
        {
            if (previousDate == null)
            {
                tempStreak = 1;
            }
            else if ((date - previousDate.Value).Days == 1)
            {
                tempStreak++;
            }
            else
            {
                if (tempStreak > longestStreak) longestStreak = tempStreak;
                tempStreak = 1;
            }
            previousDate = date;
        }
        if (tempStreak > longestStreak) longestStreak = tempStreak;

        var orderedDates = distinctDates.OrderByDescending(d => d).ToList();
        if (orderedDates.Any() && (orderedDates[0] == today || (today - orderedDates[0]).Days == 1))
        {
            currentStreak = 1;
            for (int i = 1; i < orderedDates.Count; i++)
            {
                if ((orderedDates[i - 1] - orderedDates[i]).Days == 1)
                {
                    currentStreak++;
                }
                else
                {
                    break;
                }
            }
        }

        return Ok(new StreakInfo
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            LastEntryDate = lastEntryDate,
            HasEntryToday = hasEntryToday
        });
    }
}

public class DiaryEntryDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public List<IFormFile>? Images { get; set; }
}
