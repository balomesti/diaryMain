using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using diary_api.Data;
using diary_api.Models;
using System.Security.Claims;

namespace diary_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserController(AppDbContext context)
    {
        _context = context;
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound("User not found");

        return Ok(new
        {
            user.Username,
            user.Email,
            user.ProfileImg,
            user.UserBio
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound("User not found");

        user.Username = request.Username ?? user.Username;
        user.UserBio = request.UserBio ?? user.UserBio;

        if (!string.IsNullOrEmpty(request.ProfileImg))
        {
            user.ProfileImg = request.ProfileImg;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            user.Username,
            user.Email,
            user.ProfileImg,
            user.UserBio
        });
    }

    [HttpPut("profile/username")]
    public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameRequest request)
    {
        var userId = GetUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound("User not found");

        user.Username = request.Username;
        await _context.SaveChangesAsync();

        return Ok(new { user.Username });
    }
}

public class UpdateProfileRequest
{
    public string? Username { get; set; }
    public string? ProfileImg { get; set; }
    public string? UserBio { get; set; }
}

public class UpdateUsernameRequest
{
    public string Username { get; set; } = string.Empty;
}