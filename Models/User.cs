namespace BloodConnect.Api.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Phone { get; set; } = "";
    public string City { get; set; } = "";

    // A user can be a donor (has a blood type) and a requester at the same time.
    public BloodType? BloodType { get; set; }
    public UserRole Role { get; set; } = UserRole.User;

    // Used to find nearby requests / donors.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool IsAvailableToDonate { get; set; } = true;
    public DateTime? LastDonationDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BloodRequest> Requests { get; set; } = new();
    public List<Pledge> Pledges { get; set; } = new();
}
