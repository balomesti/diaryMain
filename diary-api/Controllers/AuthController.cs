using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using diary_api.Data;
using diary_api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace diary_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }


    /// POST api/Auth/register (Creates a new user account.)
    /// Request body (JSON): { username, email, passwordHash } 
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] User user)
    {
        if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            return BadRequest("Email already exists");

        // Simple hashing for now
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        return Ok(new { Message = "Registration successful" });
    }

    /// POST api/Auth/login (Authenticates a user and returns a JWT token.)
    /// Request body (JSON): { email, password }.
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var isAdmin = user.Email.EndsWith("@office.com", StringComparison.OrdinalIgnoreCase) || 
                      user.Email.StartsWith("admin", StringComparison.OrdinalIgnoreCase);
        var token = GenerateJwtToken(user, isAdmin);
        return Ok(new { Token = token, Username = user.Username });
    }

    private string GenerateJwtToken(User user, bool isAdmin)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyWithAtLeast32Characters";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
