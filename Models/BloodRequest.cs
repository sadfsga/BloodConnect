namespace BloodConnect.Api.Models;

public class BloodRequest
{
    public int Id { get; set; }

    public int RequesterId { get; set; }
    public User Requester { get; set; } = null!;

    public string PatientName { get; set; } = "";
    public BloodType BloodType { get; set; }
    public int UnitsNeeded { get; set; } = 1;

    public string Hospital { get; set; } = "";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public Urgency Urgency { get; set; } = Urgency.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Open;

    public string ContactPhone { get; set; } = "";
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NeededBy { get; set; }

    public List<Pledge> Pledges { get; set; } = new();
}
