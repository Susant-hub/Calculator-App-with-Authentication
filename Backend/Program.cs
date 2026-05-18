using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add JWT authentication
var key = "ThisIsASecretKeyForJwtTokenThatIsAtLeast32BytesLong!"; // In production, store in config
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
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
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

// In-memory storage
var users = new List<User>();
var calculations = new List<Calculation>();
int nextUserId = 1;
int nextCalcId = 1;

// Helper to hash password (simple hash for demo - use BCrypt in production)
string HashPassword(string password) => Convert.ToBase64String(Encoding.UTF8.GetBytes(password));

// Register
app.MapPost("/api/auth/register", (RegisterRequest req) =>
{
    if (users.Any(u => u.Email == req.Email))
        return Results.BadRequest("Email already exists");

    var user = new User
    {
        Id = nextUserId++,
        Email = req.Email,
        PasswordHash = HashPassword(req.Password),
        Username = req.Username
    };
    users.Add(user);
    return Results.Ok(new { message = "Registered successfully" });
});

// Login
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    var user = users.FirstOrDefault(u => u.Email == req.Email && u.PasswordHash == HashPassword(req.Password));
    if (user == null)
        return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenKey = Encoding.UTF8.GetBytes(key);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) }),
        Expires = DateTime.UtcNow.AddDays(7),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    return Results.Ok(new { token, username = user.Username });
});

// Get history (requires auth)
app.MapGet("/api/calculations", (HttpContext ctx) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();
    var userCalcs = calculations.Where(c => c.UserId == userId.Value).OrderByDescending(c => c.Timestamp).ToList();
    return Results.Ok(userCalcs);
}).RequireAuthorization();

// Save calculation (requires auth)
app.MapPost("/api/calculations", (CalculationInput input, HttpContext ctx) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();
    var calc = new Calculation
    {
        Id = nextCalcId++,
        UserId = userId.Value,
        Expression = input.Expression,
        Result = input.Result,
        Timestamp = DateTime.UtcNow
    };
    calculations.Add(calc);
    return Results.Ok(calc);
}).RequireAuthorization();

// Clear history (requires auth)
app.MapDelete("/api/calculations", (HttpContext ctx) =>
{
    var userId = GetUserId(ctx);
    if (userId == null) return Results.Unauthorized();
    calculations.RemoveAll(c => c.UserId == userId.Value);
    return Results.Ok();
}).RequireAuthorization();

// Helper to get user ID from token
int? GetUserId(HttpContext ctx)
{
    var userIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (int.TryParse(userIdClaim, out int id)) return id;
    return null;
}

app.Run();

record CalculationInput(string Expression, string Result);