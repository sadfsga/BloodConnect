using BloodConnect.Api.Data;
using BloodConnect.Api.Dtos;
using BloodConnect.Api.Options;
using BloodConnect.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BloodConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MatchingOptions _opt;

    public ProfileController(AppDbContext db, IOptions<MatchingOptions> opt)
    {
        _db = db;
        _opt = opt.Value;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        return UserDto.From(user, _opt.MinDaysBetweenDonations);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileDto dto)
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        // Only change what the client actually sent.
        if (dto.FullName != null) user.FullName = dto.FullName.Trim();
        if (dto.Phone != null) user.Phone = dto.Phone.Trim();
        if (dto.City != null) user.City = dto.City.Trim();
        if (dto.BloodType != null) user.BloodType = dto.BloodType;
        if (dto.Latitude != null) user.Latitude = dto.Latitude;
        if (dto.Longitude != null) user.Longitude = dto.Longitude;
        if (dto.IsAvailableToDonate != null) user.IsAvailableToDonate = dto.IsAvailableToDonate.Value;

        await _db.SaveChangesAsync();
        return UserDto.From(user, _opt.MinDaysBetweenDonations);
    }

    [HttpGet("pledges")]
    public async Task<ActionResult<List<MyPledgeDto>>> MyPledges()
    {
        var userId = User.GetUserId();

        var pledges = await _db.Pledges
            .AsNoTracking()
            .Where(p => p.DonorId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.RequestId,
                p.Request.BloodType,
                p.Request.Hospital,
                p.Request.City,
                RequestStatus = p.Request.Status,
                p.Status,
                p.CreatedAt
            })
            .ToListAsync();

        return pledges
            .Select(p => new MyPledgeDto(
                p.Id,
                p.RequestId,
                BloodCompatibility.Label(p.BloodType),
                p.Hospital,
                p.City,
                p.RequestStatus,
                p.Status,
                p.CreatedAt))
            .ToList();
    }
}
