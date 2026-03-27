using Microsoft.EntityFrameworkCore;
using diary_api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100MB
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Diary API", Version = "v1" });

    options.TagActionsBy(_ => new[] { "default" });
    options.CustomOperationIds(apiDesc =>
    {
        var controller = apiDesc.ActionDescriptor.RouteValues["controller"];
        var action = apiDesc.ActionDescriptor.RouteValues["action"];
        return $"{controller}Controller.{action}";
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

// Database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=diary.db"));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyWithAtLeast32Characters";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = ClaimTypes.Role
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "docs/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "docs";
        c.SwaggerEndpoint("/docs/v1/swagger.json", "Diary API v1");
        c.DisplayOperationId();
        c.EnableDeepLinking();
    });
}

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created and migrations applied
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    try
    {
        // Fix for missing VideoUrl column in existing database
        try { db.Database.ExecuteSqlRaw("ALTER TABLE NewsPosts ADD COLUMN VideoUrl TEXT;"); } catch { }
        
        // Fix for multiple images support
        try { db.Database.ExecuteSqlRaw("ALTER TABLE DiaryEntries ADD COLUMN ImageUrls TEXT;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE DiaryEntries SET ImageUrls = ImageUrl WHERE ImageUrls IS NULL OR ImageUrls = '';"); } catch { }

        // Fix for User profile columns
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN ProfileImg TEXT;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN UserBio TEXT;"); } catch { }

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS KeyHighlights (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT,
                Icon TEXT,
                Time TEXT,
                CreatedAt TEXT NOT NULL,
                DiaryEntryId INTEGER NOT NULL,
                FOREIGN KEY (DiaryEntryId) REFERENCES DiaryEntries(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS NewsPosts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                ImageUrl TEXT,
                VideoUrl TEXT,
                Author TEXT,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Announcements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Message TEXT NOT NULL,
                Author TEXT,
                CreatedAt TEXT NOT NULL
            );
        ");
    }
    catch { }
}

app.Run();
