using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ScheduleController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SiteDutyDto>>> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _context.SiteDuties.AsQueryable();

        if (from.HasValue)
            query = query.Where(d => d.EndDate >= from.Value);
        if (to.HasValue)
            query = query.Where(d => d.StartDate <= to.Value);

        var duties = await query
            .OrderBy(d => d.StartDate)
            .Select(d => new SiteDutyDto
            {
                Id = d.Id,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                TeamName = d.TeamName,
                Notes = d.Notes
            })
            .ToListAsync();

        return Ok(duties);
    }

    [HttpPost]
    public async Task<ActionResult<SiteDutyDto>> Create([FromBody] CreateSiteDutyRequest request)
    {
        var entity = new SiteDuty
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TeamName = request.TeamName,
            Notes = request.Notes
        };

        _context.SiteDuties.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(new SiteDutyDto
        {
            Id = entity.Id,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            TeamName = entity.TeamName,
            Notes = entity.Notes
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var entity = await _context.SiteDuties.FindAsync(id);
        if (entity == null) return NotFound();

        _context.SiteDuties.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("generate")]
    public async Task<ActionResult<int>> Generate([FromBody] GenerateScheduleRequest request)
    {
        var duties = DutyScheduleGenerator.Generate(request.From, request.To);
        _context.SiteDuties.AddRange(duties);
        await _context.SaveChangesAsync();
        return Ok(new { count = duties.Count });
    }
}
