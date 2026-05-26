using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var key = builder.Configuration["JwtKey"] ?? "ThisIsASecretKeyForJwtTokenThatIsAtLeast32BytesLong!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://suscalc.netlify.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var dbPath = builder.Configuration["DbPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "data", "suscalc.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();

// ── HELPERS ──────────────────────────────────────────────────────────────────

string HashPassword(string password)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
    return Convert.ToHexString(bytes).ToLowerInvariant();
}

string GenerateToken(User user)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenKey = Encoding.UTF8.GetBytes(key);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username)
        }),
        Expires = DateTime.UtcNow.AddDays(7),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(tokenKey),
            SecurityAlgorithms.HmacSha256Signature)
    };
    return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
}

int? GetUserId(HttpContext ctx)
{
    var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return int.TryParse(claim, out int id) ? id : null;
}

// ── REGISTER ─────────────────────────────────────────────────────────────────

app.MapPost("/api/auth/register", async (RegisterRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Email)) return Results.BadRequest("Email is required.");
    if (string.IsNullOrWhiteSpace(req.Password)) return Results.BadRequest("Password is required.");
    if (string.IsNullOrWhiteSpace(req.Username)) return Results.BadRequest("Username is required.");
    if (req.Password.Length < 6) return Results.BadRequest("Password must be at least 6 characters.");

    var email = req.Email.Trim().ToLowerInvariant();

    if (await db.Users.AnyAsync(u => u.Email == email))
        return Results.BadRequest("An account with this email already exists.");

    var user = new User
    {
        Email = email,
        PasswordHash = HashPassword(req.Password),
        Username = req.Username.Trim()
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { token = GenerateToken(user), username = user.Username });
});

// ── LOGIN ─────────────────────────────────────────────────────────────────────

app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest("Email and password are required.");

    var email = req.Email.Trim().ToLowerInvariant();
    var hash = HashPassword(req.Password);
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hash);

    if (user == null)
        return Results.BadRequest("Incorrect email or password.");

    return Results.Ok(new { token = GenerateToken(user), username = user.Username });
});

// ── GET HISTORY ───────────────────────────────────────────────────────────────

app.MapGet("/api/calculations", async (HttpContext ctx, AppDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var calcs = await db.Calculations
        .Where(c => c.UserId == userId.Value)
        .OrderByDescending(c => c.Timestamp)
        .ToListAsync();

    return Results.Ok(calcs);
}).RequireAuthorization();

// ── SAVE CALCULATION ──────────────────────────────────────────────────────────

app.MapPost("/api/calculations", async (CalculationInput input, HttpContext ctx, AppDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(input.Expression) || string.IsNullOrWhiteSpace(input.Result))
        return Results.BadRequest("Expression and result are required.");

    var calc = new Calculation
    {
        UserId = userId.Value,
        Expression = input.Expression,
        Result = input.Result,
        Timestamp = DateTime.UtcNow
    };

    db.Calculations.Add(calc);
    await db.SaveChangesAsync();

    return Results.Ok(calc);
}).RequireAuthorization();

// ── CLEAR HISTORY ─────────────────────────────────────────────────────────────

app.MapDelete("/api/calculations", async (HttpContext ctx, AppDbContext db) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var calcs = await db.Calculations.Where(c => c.UserId == userId.Value).ToListAsync();
    db.Calculations.RemoveRange(calcs);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "History cleared." });
}).RequireAuthorization();

// ── HEALTH ────────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "Alive" }));

app.Run();

record CalculationInput(string Expression, string Result);