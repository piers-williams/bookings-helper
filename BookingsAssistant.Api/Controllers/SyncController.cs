using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Sync()
    {
        // TODO: Implement sync logic in Phase 1D/1E
        // For now, just return success
        await Task.Delay(500); // Simulate API call

        return Ok(new
        {
            EmailsSynced = 2,
            BookingsSynced = 3,
            CommentsSynced = 2,
            LastSync = DateTime.UtcNow
        });
    }
}
