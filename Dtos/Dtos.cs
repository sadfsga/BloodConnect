using System.ComponentModel.DataAnnotations;
using BloodConnect.Api.Models;
using BloodConnect.Api.Services;

namespace BloodConnect.Api.Dtos;

// ---------- Auth & profile ----------

public class RegisterDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Required, Phone]
    public string Phone { get; set; } = "";

    [Required, StringLength(80)]
    public string City { get; set; } = "";

    // Optional: only people who want to donate need a blood type.
    public BloodType? BloodType { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }
}

public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class UpdateProfileDto
{
    [StringLength(100, MinimumLength = 2)]
    public string? FullName { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    public BloodType? BloodType { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public bool? IsAvailableToDonate { get; set; }
}

public record UserDto(
    int Id,
    string FullName,
    string Email,
    string Phone,
    string City,
    BloodType? BloodType,
    string? BloodTypeLabel,
    UserRole Role,
    double? Latitude,
    double? Longitude,
    bool IsAvailableToDonate,
    DateTime? LastDonationDate,
    bool IsEligibleToDonate)
{
    public static UserDto From(User u, int minDaysBetweenDonations)
    {
        var eligible = u.BloodType != null
                       && u.IsAvailableToDonate
                       && (u.LastDonationDate == null
                           || (DateTime.UtcNow - u.LastDonationDate.Value).TotalDays >= minDaysBetweenDonations);

        return new UserDto(
            u.Id,
            u.FullName,
            u.Email,
            u.Phone,
            u.City,
            u.BloodType,
            u.BloodType == null ? null : BloodCompatibility.Label(u.BloodType.Value),
            u.Role,
            u.Latitude,
            u.Longitude,
            u.IsAvailableToDonate,
            u.LastDonationDate,
            eligible);
    }
}

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------- Blood requests ----------

public class CreateRequestDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string PatientName { get; set; } = "";

    [Required]
    public BloodType? BloodType { get; set; }

    [Range(1, 10)]
    public int UnitsNeeded { get; set; } = 1;

    [Required, StringLength(150, MinimumLength = 2)]
    public string Hospital { get; set; } = "";

    [Required, StringLength(80, MinimumLength = 2)]
    public string City { get; set; } = "";

    [Required, Range(-90, 90)]
    public double? Latitude { get; set; }

    [Required, Range(-180, 180)]
    public double? Longitude { get; set; }

    public Urgency Urgency { get; set; } = Urgency.Normal;

    [Required, Phone]
    public string ContactPhone { get; set; } = "";

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime? NeededBy { get; set; }
}

public class RequestQuery
{
    public BloodType? BloodType { get; set; }
    public string? City { get; set; }

    // Defaults to Open when not provided.
    public RequestStatus? Status { get; set; }

    // Search around a point.
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public double? RadiusKm { get; set; }

    // Only requests that MY blood type can help.
    public bool CompatibleWithMe { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 20;
}

public record RequestSummaryDto(
    int Id,
    BloodType BloodType,
    string BloodTypeLabel,
    int UnitsNeeded,
    int UnitsFulfilled,
    string Hospital,
    string City,
    double Latitude,
    double Longitude,
    Urgency Urgency,
    RequestStatus Status,
    DateTime CreatedAt,
    DateTime? NeededBy,
    double? DistanceKm,
    bool CanIDonate);

public record PledgeDto(
    int Id,
    string DonorName,
    string DonorPhone,
    BloodType? DonorBloodType,
    PledgeStatus Status,
    DateTime CreatedAt);

public record RequestDetailsDto(
    int Id,
    BloodType BloodType,
    string BloodTypeLabel,
    int UnitsNeeded,
    int UnitsFulfilled,
    string Hospital,
    string City,
    double Latitude,
    double Longitude,
    Urgency Urgency,
    RequestStatus Status,
    DateTime CreatedAt,
    DateTime? NeededBy,
    string? Notes,
    string RequesterName,
    // Only the owner (or an admin) sees the patient name.
    string? PatientName,
    // Only the owner, an admin, or a donor who pledged sees the phone number.
    string? ContactPhone,
    bool CanIDonate,
    PledgeStatus? MyPledgeStatus,
    // Only the owner / admin: how many eligible donors are near this request.
    int? MatchingDonorsCount,
    // Only the owner / admin: who pledged.
    List<PledgeDto>? Pledges);

public record MyPledgeDto(
    int Id,
    int RequestId,
    string BloodTypeLabel,
    string Hospital,
    string City,
    RequestStatus RequestStatus,
    PledgeStatus Status,
    DateTime CreatedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record StatsDto(int OpenRequests, int FulfilledRequests, int RegisteredDonors, int ConfirmedDonations);
