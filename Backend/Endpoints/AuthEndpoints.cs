using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.Helpers;

namespace Backend.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app, string jwtKey)
    {
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
                PasswordHash = PasswordHasher.Hash(req.Password),
                Username = req.Username.Trim()
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = TokenGenerator.GenerateToken(user, jwtKey);
            return Results.Ok(new { token, username = user.Username });
        });

        app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest("Email and password are required.");

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
                return Results.BadRequest("Incorrect email or password.");

            var token = TokenGenerator.GenerateToken(user, jwtKey);
            return Results.Ok(new { token, username = user.Username });
        });
    }
}