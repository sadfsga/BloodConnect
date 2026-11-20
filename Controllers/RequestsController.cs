using BloodConnect.Api.Data;
using BloodConnect.Api.Dtos;
using BloodConnect.Api.Models;
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
public class RequestsController : ControllerBase
{
    // Anti-spam: nobody can keep more than this many open requests.
    private const int MaxOpenRequestsPerUser = 5;

    private readonly AppDbContext _db;
    private readonly DonorMatchingService _matching;
    private readonly MatchingOptions _opt;

    public RequestsController(AppDbContext db, DonorMatchingService matching, IOptions<MatchingOptions> opt)
    {
        _db = db;
        _matching = matching;
        _opt = opt.Value;
    }

    // A request + how many units were already donated (used for list queries).
    private sealed class RequestRow
    {
        public BloodRequest Request { get; set; } = null!;
        public int Fulfilled { get; set; }
    }

    // ------------------------------------------------------------------
    // GET /api/requests  -> browse requests (filters, nearby search, paging)
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<PagedResult<RequestSummaryDto>>> List([FromQuery] RequestQuery q)
    {
        var me = await GetMeAsync();
        if (me == null) return Unauthorized();

        var status = q.Status ?? RequestStatus.Open;
        var query = _db.BloodRequests.AsNoTracking().Where(r => r.Status == status);

        if (q.BloodType != null)
        {
            var wanted = q.BloodType.Value;
            query = query.Where(r => r.BloodType == wanted);
        }

        if (!string.IsNullOrWhiteSpace(q.City))
        {
            var city = q.City.Trim();
            query = query.Where(r => r.City == city);
        }

        if (q.CompatibleWithMe)
        {
            if (me.BloodType == null)
                return BadRequest(new { message = "Set your blood type in your profile first." });

            var targets = BloodCompatibility.RecipientsFor(me.BloodType.Value).ToList();
            query = query.Where(r => targets.Contains(r.BloodType));
        }

        var hasGeo = q.Lat != null && q.Lng != null;

        // ---- Nearby search: rectangle in SQL, exact distance in memory ----
        if (hasGeo)
        {
            var lat = q.Lat!.Value;
            var lng = q.Lng!.Value;
            var radius = q.RadiusKm ?? _opt.RadiusKm;

            var box = GeoHelper.BoundingBox(lat, lng, radius);
            var minLat = box.MinLat;
            var maxLat = box.MaxLat;
            var minLng = box.MinLng;
            var maxLng = box.MaxLng;

            var rows = await query
                .Where(r => r.Latitude >= minLat && r.Latitude <= maxLat
                            && r.Longitude >= minLng && r.Longitude <= maxLng)
                .Select(r => new RequestRow
                {
                    Request = r,
                    Fulfilled = r.Pledges.Count(p => p.Status == PledgeStatus.Donated)
                })
                .ToListAsync();

            var nearby = rows
                .Select(x => new
                {
                    x.Request,
                    x.Fulfilled,
                    Distance = GeoHelper.DistanceKm(lat, lng, x.Request.Latitude, x.Request.Longitude)
                })
                .Where(x => x.Distance <= radius)
                .OrderByDescending(x => UrgencyRank(x.Request.Urgency))
                .ThenBy(x => x.Distance)
                .ToList();

            var pageItems = nearby
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(x => ToSummary(x.Request, x.Fulfilled, x.Distance, me))
                .ToList();

            return new PagedResult<RequestSummaryDto>(pageItems, q.Page, q.PageSize, nearby.Count);
        }

        // ---- Normal listing: most urgent first, then newest ----
        var total = await query.CountAsync();

        var page = await query
            .OrderByDescending(r => r.Urgency == Urgency.Critical ? 2 : (r.Urgency == Urgency.Urgent ? 1 : 0))
            .ThenByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(r => new RequestRow
            {
                Request = r,
                Fulfilled = r.Pledges.Count(p => p.Status == PledgeStatus.Donated)
            })
            .ToListAsync();

        var items = page.Select(x => ToSummary(x.Request, x.Fulfilled, null, me)).ToList();
        return new PagedResult<RequestSummaryDto>(items, q.Page, q.PageSize, total);
    }

    // ------------------------------------------------------------------
    // GET /api/requests/mine -> requests I created
    // ------------------------------------------------------------------
    [HttpGet("mine")]
    public async Task<ActionResult<List<RequestSummaryDto>>> Mine()
    {
        var me = await GetMeAsync();
        if (me == null) return Unauthorized();

        var rows = await _db.BloodRequests
            .AsNoTracking()
            .Where(r => r.RequesterId == me.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RequestRow
            {
                Request = r,
                Fulfilled = r.Pledges.Count(p => p.Status == PledgeStatus.Donated)
            })
            .ToListAsync();

        return rows.Select(x => ToSummary(x.Request, x.Fulfilled, null, me)).ToList();
    }

    // ------------------------------------------------------------------
    // GET /api/requests/5 -> details (some fields are private)
    // ------------------------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RequestDetailsDto>> GetById(int id)
    {
        var dto = await BuildDetailsAsync(id);
        if (dto == null) return NotFound();
        return dto;
    }

    // ------------------------------------------------------------------
    // POST /api/requests -> ask for blood
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<RequestDetailsDto>> Create(CreateRequestDto dto)
    {
        var userId = User.GetUserId();

        if (dto.NeededBy != null && dto.NeededBy.Value.ToUniversalTime() <= DateTime.UtcNow)
            return BadRequest(new { message = "NeededBy must be in the future." });

        var openCount = await _db.BloodRequests
            .CountAsync(r => r.RequesterId == userId && r.Status == RequestStatus.Open);
        if (openCount >= MaxOpenRequestsPerUser)
            return BadRequest(new { message = $"You can have at most {MaxOpenRequestsPerUser} open requests." });

        var request = new BloodRequest
        {
            RequesterId = userId,
            PatientName = dto.PatientName.Trim(),
            BloodType = dto.BloodType!.Value,
            UnitsNeeded = dto.UnitsNeeded,
            Hospital = dto.Hospital.Trim(),
            City = dto.City.Trim(),
            Latitude = dto.Latitude!.Value,
            Longitude = dto.Longitude!.Value,
            Urgency = dto.Urgency,
            Status = RequestStatus.Open,
            ContactPhone = dto.ContactPhone.Trim(),
            Notes = dto.Notes?.Trim(),
            NeededBy = dto.NeededBy?.ToUniversalTime()
        };

        _db.BloodRequests.Add(request);
        await _db.SaveChangesAsync();

        var details = await BuildDetailsAsync(request.Id);
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, details);
    }

    // ------------------------------------------------------------------
    // POST /api/requests/5/pledge -> "I will donate"
    // ------------------------------------------------------------------
    [HttpPost("{id:int}/pledge")]
    public async Task<ActionResult<RequestDetailsDto>> Pledge(int id)
    {
        var me = await _db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (me == null) return Unauthorized();

        var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Open)
            return BadRequest(new { message = "This request is no longer open." });
        if (request.RequesterId == me.Id)
            return BadRequest(new { message = "You cannot pledge to your own request." });
        if (me.BloodType == null)
            return BadRequest(new { message = "Set your blood type in your profile first." });
        if (!BloodCompatibility.CanDonate(me.BloodType.Value, request.BloodType))
            return BadRequest(new { message = "Your blood type is not compatible with this request." });
        if (!_matching.IsEligible(me))
            return BadRequest(new { message = "You are not eligible to donate right now (availability or last donation date)." });

        var existing = await _db.Pledges.FirstOrDefaultAsync(p => p.RequestId == id && p.DonorId == me.Id);
        if (existing != null && existing.Status != PledgeStatus.Cancelled)
            return Conflict(new { message = "You already pledged to this request." });

        if (existing != null)
        {
            // The donor cancelled before and now changed their mind.
            existing.Status = PledgeStatus.Pledged;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Pledges.Add(new Pledge { RequestId = id, DonorId = me.Id });
        }

        await _db.SaveChangesAsync();
        return await BuildDetailsAsync(id) ?? (ActionResult<RequestDetailsDto>)NotFound();
    }

    // ------------------------------------------------------------------
    // DELETE /api/requests/5/pledge -> cancel my pledge
    // ------------------------------------------------------------------
    [HttpDelete("{id:int}/pledge")]
    public async Task<IActionResult> CancelPledge(int id)
    {
        var userId = User.GetUserId();
        var pledge = await _db.Pledges.FirstOrDefaultAsync(p =>
            p.RequestId == id && p.DonorId == userId && p.Status == PledgeStatus.Pledged);
        if (pledge == null) return NotFound(new { message = "No active pledge found." });

        pledge.Status = PledgeStatus.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------------
    // POST /api/requests/5/pledges/9/confirm -> the owner confirms the donor really donated
    // ------------------------------------------------------------------
    [HttpPost("{id:int}/pledges/{pledgeId:int}/confirm")]
    public async Task<ActionResult<RequestDetailsDto>> ConfirmDonation(int id, int pledgeId)
    {
        var userId = User.GetUserId();

        var request = await _db.BloodRequests
            .Include(r => r.Pledges)
            .ThenInclude(p => p.Donor)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        if (request.RequesterId != userId && !User.IsInRole("Admin")) return Forbid();
        if (request.Status != RequestStatus.Open)
            return BadRequest(new { message = "This request is not open." });

        var pledge = request.Pledges.FirstOrDefault(p => p.Id == pledgeId);
        if (pledge == null) return NotFound();
        if (pledge.Status != PledgeStatus.Pledged)
            return BadRequest(new { message = "Only active pledges can be confirmed." });

        pledge.Status = PledgeStatus.Donated;
        // The donor must now wait before donating again.
        pledge.Donor.LastDonationDate = DateTime.UtcNow;

        var donated = request.Pledges.Count(p => p.Status == PledgeStatus.Donated);
        if (donated >= request.UnitsNeeded)
            request.Status = RequestStatus.Fulfilled;

        await _db.SaveChangesAsync();
        return await BuildDetailsAsync(id) ?? (ActionResult<RequestDetailsDto>)NotFound();
    }

    // ------------------------------------------------------------------
    // POST /api/requests/5/cancel -> the owner closes the request
    // ------------------------------------------------------------------
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<RequestDetailsDto>> Cancel(int id)
    {
        var userId = User.GetUserId();

        var request = await _db.BloodRequests
            .Include(r => r.Pledges)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        if (request.RequesterId != userId && !User.IsInRole("Admin")) return Forbid();
        if (request.Status != RequestStatus.Open)
            return BadRequest(new { message = "Only open requests can be cancelled." });

        request.Status = RequestStatus.Cancelled;
        foreach (var p in request.Pledges.Where(p => p.Status == PledgeStatus.Pledged))
            p.Status = PledgeStatus.Cancelled;

        await _db.SaveChangesAsync();
        return await BuildDetailsAsync(id) ?? (ActionResult<RequestDetailsDto>)NotFound();
    }

    // ==================================================================
    // helpers
    // ==================================================================

    private Task<User?> GetMeAsync()
    {
        var userId = User.GetUserId();
        return _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    private static int UrgencyRank(Urgency u)
    {
        return u == Urgency.Critical ? 2 : (u == Urgency.Urgent ? 1 : 0);
    }

    private static RequestSummaryDto ToSummary(BloodRequest r, int fulfilled, double? distanceKm, User me)
    {
        var canDonate = r.RequesterId != me.Id
                        && me.BloodType != null
                        && BloodCompatibility.CanDonate(me.BloodType.Value, r.BloodType);

        return new RequestSummaryDto(
            r.Id,
            r.BloodType,
            BloodCompatibility.Label(r.BloodType),
            r.UnitsNeeded,
            fulfilled,
            r.Hospital,
            r.City,
            r.Latitude,
            r.Longitude,
            r.Urgency,
            r.Status,
            r.CreatedAt,
            r.NeededBy,
            distanceKm == null ? null : Math.Round(distanceKm.Value, 1),
            canDonate);
    }

    // Builds the details view and hides private fields from people who should not see them.
    private async Task<RequestDetailsDto?> BuildDetailsAsync(int id)
    {
        var userId = User.GetUserId();

        var r = await _db.BloodRequests
            .AsNoTracking()
            .Include(x => x.Requester)
            .Include(x => x.Pledges)
            .ThenInclude(p => p.Donor)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return null;

        var me = await GetMeAsync();
        if (me == null) return null;

        var isOwner = r.RequesterId == userId || User.IsInRole("Admin");
        var myPledge = r.Pledges.FirstOrDefault(p => p.DonorId == userId);
        var hasActivePledge = myPledge != null && myPledge.Status != PledgeStatus.Cancelled;
        var fulfilled = r.Pledges.Count(p => p.Status == PledgeStatus.Donated);

        int? matching = null;
        List<PledgeDto>? pledges = null;
        if (isOwner)
        {
            matching = await _matching.CountEligibleDonorsAsync(r);
            pledges = r.Pledges
                .Where(p => p.Status != PledgeStatus.Cancelled)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new PledgeDto(p.Id, p.Donor.FullName, p.Donor.Phone, p.Donor.BloodType, p.Status, p.CreatedAt))
                .ToList();
        }

        var canDonate = r.RequesterId != me.Id
                        && me.BloodType != null
                        && BloodCompatibility.CanDonate(me.BloodType.Value, r.BloodType);

        return new RequestDetailsDto(
            r.Id,
            r.BloodType,
            BloodCompatibility.Label(r.BloodType),
            r.UnitsNeeded,
            fulfilled,
            r.Hospital,
            r.City,
            r.Latitude,
            r.Longitude,
            r.Urgency,
            r.Status,
            r.CreatedAt,
            r.NeededBy,
            r.Notes,
            r.Requester.FullName,
            isOwner ? r.PatientName : null,
            (isOwner || hasActivePledge) ? r.ContactPhone : null,
            canDonate,
            myPledge?.Status,
            matching,
            pledges);
    }
}
