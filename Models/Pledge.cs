namespace BloodConnect.Api.Models;

// A donor's promise to help one specific request.
public class Pledge
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public BloodRequest Request { get; set; } = null!;

    public int DonorId { get; set; }
    public User Donor { get; set; } = null!;

    public PledgeStatus Status { get; set; } = PledgeStatus.Pledged;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
