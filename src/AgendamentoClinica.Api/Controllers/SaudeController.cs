using Microsoft.AspNetCore.Mvc;

namespace AgendamentoClinica.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SaudeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
