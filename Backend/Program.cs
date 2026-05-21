using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var key = "ThisIsASecretKeyForJwtTokenThatIsAtLeast32BytesLong!";

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
                "https://suscalc.netlify.app"   // <--- your Netlify frontend
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();

// ── FILE PATHS ───
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var usersFile = Path.Combine(dataDir, "users.json");
var calcsFile = Path.Combine(dataDir, "calculations.json");

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

// ── PERSISTENCE HELPERS ───
List<User> LoadUsers()
{
    if (!File.Exists(usersFile)) return [];
    try { return JsonSerializer.Deserialize<List<User>>(File.ReadAllText(usersFile)) ?? []; }
    catch { return []; }
}

void SaveUsers(List<User> users) =>
    File.WriteAllText(usersFile, JsonSerializer.Serialize(users, jsonOptions));

List<Calculation> LoadCalcs()
{
    if (!File.Exists(calcsFile)) return [];
    try { return JsonSerializer.Deserialize<List<Calculation>>(File.ReadAllText(calcsFile)) ?? []; }
    catch { return []; }
}

void SaveCalcs(List<Calculation> calcs) =>
    File.WriteAllText(calcsFile, JsonSerializer.Serialize(calcs, jsonOptions));

// ── HELPERS ──
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

// ── REGISTER ──
app.MapPost("/api/auth/register", (RegisterRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Email)) return Results.BadRequest("Email is required.");
    if (string.IsNullOrWhiteSpace(req.Password)) return Results.BadRequest("Password is required.");
    if (string.IsNullOrWhiteSpace(req.Username)) return Results.BadRequest("Username is required.");
    if (req.Password.Length < 6) return Results.BadRequest("Password must be at least 6 characters.");

    var users = LoadUsers();

    if (users.Any(u => u.Email.Equals(req.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
        return Results.BadRequest("An account with this email already exists.");

    var newId = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
    var user = new User
    {
        Id = newId,
        Email = req.Email.Trim().ToLowerInvariant(),
        PasswordHash = HashPassword(req.Password),
        Username = req.Username.Trim()
    };
    users.Add(user);
    SaveUsers(users);

    return Results.Ok(new { token = GenerateToken(user), username = user.Username });
});

// ── LOGIN ──
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest("Email and password are required.");

    var users = LoadUsers();
    var hash = HashPassword(req.Password);
    var user = users.FirstOrDefault(u =>
        u.Email.Equals(req.Email.Trim().ToLowerInvariant(), StringComparison.Ordinal)
        && u.PasswordHash == hash);

    if (user == null)
        return Results.BadRequest("Incorrect email or password.");

    return Results.Ok(new { token = GenerateToken(user), username = user.Username });
});

// ── GET HISTORY ──
app.MapGet("/api/calculations", (HttpContext ctx) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var calcs = LoadCalcs()
        .Where(c => c.UserId == userId.Value)
        .OrderByDescending(c => c.Timestamp)
        .ToList();

    return Results.Ok(calcs);
}).RequireAuthorization();

// ── SAVE CALCULATION ──
app.MapPost("/api/calculations", (CalculationInput input, HttpContext ctx) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(input.Expression) || string.IsNullOrWhiteSpace(input.Result))
        return Results.BadRequest("Expression and result are required.");

    var calcs = LoadCalcs();
    var newId = calcs.Count > 0 ? calcs.Max(c => c.Id) + 1 : 1;
    var calc = new Calculation
    {
        Id = newId,
        UserId = userId.Value,
        Expression = input.Expression,
        Result = input.Result,
        Timestamp = DateTime.UtcNow
    };
    calcs.Add(calc);
    SaveCalcs(calcs);

    return Results.Ok(calc);
}).RequireAuthorization();

// ── CLEAR HISTORY ───
app.MapDelete("/api/calculations", (HttpContext ctx) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();

    var calcs = LoadCalcs();
    calcs.RemoveAll(c => c.UserId == userId.Value);
    SaveCalcs(calcs);

    return Results.Ok(new { message = "History cleared." });
}).RequireAuthorization();

app.Run();

record CalculationInput(string Expression, string Result);