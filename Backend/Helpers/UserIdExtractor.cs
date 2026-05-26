using System.Security.Claims;

namespace Backend.Helpers;

public static class UserIdExtractor
{
    public static int? GetUserId(HttpContext ctx)
    {
        var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : null;
    }
}