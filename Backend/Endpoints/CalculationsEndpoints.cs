using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.Helpers;

namespace Backend.Endpoints;

public static class CalculationsEndpoints
{
    public static void MapCalculationsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/calculations", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = UserIdExtractor.GetUserId(ctx);
            if (userId == null) return Results.Unauthorized();

            var calcs = await db.Calculations
                .Where(c => c.UserId == userId.Value)
                .OrderByDescending(c => c.Timestamp)
                .ToListAsync();

            return Results.Ok(calcs);
        }).RequireAuthorization();

        app.MapPost("/api/calculations", async (CalculationInput input, HttpContext ctx, AppDbContext db) =>
        {
            var userId = UserIdExtractor.GetUserId(ctx);
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

        app.MapDelete("/api/calculations", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = UserIdExtractor.GetUserId(ctx);
            if (userId == null) return Results.Unauthorized();

            var calcs = await db.Calculations.Where(c => c.UserId == userId.Value).ToListAsync();
            db.Calculations.RemoveRange(calcs);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "History cleared." });
        }).RequireAuthorization();
    }
}