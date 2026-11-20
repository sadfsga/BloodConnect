using BloodConnect.Api.Data;
using BloodConnect.Api.Models;
using BloodConnect.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BloodConnect.Api.Services;

// The "brain" of the app: decides who can donate and who is near a request.
public class DonorMatchingService
{
    private readonly AppDbContext _db;
    private readonly MatchingOptions _opt;

    public DonorMatchingService(AppDbContext db, IOptions<MatchingOptions> opt)
    {
        _db = db;
        _opt = opt.Value;
    }

    // Has enough time passed since the last donation?
    public bool HasWaitedEnough(DateTime? lastDonationDate)
    {
        if (lastDonationDate == null) return true;
        return (DateTime.UtcNow - lastDonationDate.Value).TotalDays >= _opt.MinDaysBetweenDonations;
    }

    public bool IsEligible(User u)
    {
        return u.BloodType != null && u.IsAvailableToDonate && HasWaitedEnough(u.LastDonationDate);
    }

    // How many people could help this request right now?
    // We return only a number, never the donors' identities (privacy).
    public async Task<int> CountEligibleDonorsAsync(BloodRequest r)
    {
        var donorTypes = BloodCompatibility.DonorsFor(r.BloodType)
            .Select(t => (BloodType?)t)
            .ToList();

        var box = GeoHelper.BoundingBox(r.Latitude, r.Longitude, _opt.RadiusKm);
        var minLat = box.MinLat;
        var maxLat = box.MaxLat;
        var minLng = box.MinLng;
        var maxLng = box.MaxLng;

        var candidates = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id != r.RequesterId
                        && u.IsAvailableToDonate
                        && donorTypes.Contains(u.BloodType)
                        && u.Latitude != null && u.Longitude != null
                        && u.Latitude >= minLat && u.Latitude <= maxLat
                        && u.Longitude >= minLng && u.Longitude <= maxLng)
            .Select(u => new { u.Latitude, u.Longitude, u.LastDonationDate })
            .ToListAsync();

        return candidates.Count(c =>
            HasWaitedEnough(c.LastDonationDate)
            && GeoHelper.DistanceKm(r.Latitude, r.Longitude, c.Latitude!.Value, c.Longitude!.Value) <= _opt.RadiusKm);
    }
}
