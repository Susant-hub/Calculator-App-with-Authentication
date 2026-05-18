using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/math")]
public class MathController : ControllerBase
{
    [HttpGet("add")]
    public int Add(int x, int y)
    {
        return x + y;
    }
}