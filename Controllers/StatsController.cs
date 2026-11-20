using BloodConnect.Api.Data;
using BloodConnect.Api.Dtos;
using BloodConnect.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodConnect.Api.Controllers;

// Public numbers for the landing page ("1,240 donors, 87 lives helped...").
// They are only totals, so they leak no personal data.
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<StatsDto>> Get()
    {
        var open = await _db.BloodRequests.CountAsync(r => r.Status == RequestStatus.Open);
        var fulfilled = await _db.BloodRequests.CountAsync(r => r.Status == RequestStatus.Fulfilled);
        var donors = await _db.Users.CountAsync(u => u.BloodType != null);
        var donations = await _db.Pledges.CountAsync(p => p.Status == PledgeStatus.Donated);

        return new StatsDto(open, fulfilled, donors, donations);
    }
}
