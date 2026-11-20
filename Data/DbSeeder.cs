using BloodConnect.Api.Models;
using BloodConnect.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BloodConnect.Api.Data;

// Fills an empty database with fake-but-realistic demo data so the map is alive on first run.
// It only runs in Development, and only once (it looks for a marker e-mail domain).
// Every demo account uses the password:  Demo@12345
public static class DbSeeder
{
    private const string DemoDomain = "@demo.bloodconnect";

    // (area name, latitude, longitude) - around Greater Cairo
    private static readonly (string Area, double Lat, double Lng)[] Areas =
    {
        ("Nasr City", 30.0561, 31.3300),
        ("Maadi", 29.9602, 31.2569),
        ("Heliopolis", 30.0900, 31.3220),
        ("Downtown", 30.0444, 31.2357),
        ("Giza", 30.0131, 31.2089),
        ("Dokki", 30.0384, 31.2118),
        ("Helwan", 29.8967, 31.3018),
        ("New Cairo", 30.0074, 31.4913),
        ("Shubra", 30.1128, 31.2447),
        ("6th of October", 29.9285, 30.9188)
    };

    private static readonly string[] Names =
    {
        "Ahmed Hassan", "Mona Adel", "Omar Khaled", "Nour Samir", "Youssef Ali", "Salma Ibrahim",
        "Karim Mostafa", "Hana Tarek", "Mahmoud Fathy", "Laila Hamdy", "Mostafa Reda", "Dina Magdy",
        "Amr Sherif", "Farida Nabil", "Hossam Ezzat", "Yasmin Gamal", "Tamer Saeed", "Rana Emad",
        "Khaled Wael", "Mariam Essam", "Ziad Haitham", "Nada Osama", "Bassem Fouad", "Heba Lotfy"
    };

    private static readonly string[] Hospitals =
    {
        "Nile Care Hospital (demo)", "Al Salam Demo Hospital", "Cairo Heart Demo Center",
        "Delta Demo Medical", "Pyramid Demo Hospital", "Sunrise Demo Clinic"
    };

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.Email.EndsWith(DemoDomain))) return;

        var rnd = new Random(42); // fixed seed: same demo data every time
        var now = DateTime.UtcNow;
        var hash = BCrypt.Net.BCrypt.HashPassword("Demo@12345");
        var allTypes = Enum.GetValues<BloodType>();

        // ---- donors ----
        var users = new List<User>();
        for (var i = 0; i < Names.Length; i++)
        {
            var area = Areas[i % Areas.Length];
            var user = new User
            {
                FullName = Names[i],
                Email = $"demo{i + 1}{DemoDomain}",
                PasswordHash = hash,
                Phone = $"+2010000{i + 1:D5}",
                City = "Cairo",
                BloodType = allTypes[rnd.Next(allTypes.Length)],
                Latitude = area.Lat + (rnd.NextDouble() - 0.5) * 0.02,
                Longitude = area.Lng + (rnd.NextDouble() - 0.5) * 0.02,
                IsAvailableToDonate = i % 7 != 0,                      // a few are unavailable
                LastDonationDate = i % 5 == 0 ? now.AddDays(-rnd.Next(5, 40)) : null, // a few donated recently
                CreatedAt = now.AddDays(-rnd.Next(5, 90))
            };
            users.Add(user);
        }
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        // ---- requests ----
        var urgencies = new[] { Urgency.Critical, Urgency.Urgent, Urgency.Normal, Urgency.Urgent, Urgency.Normal, Urgency.Critical, Urgency.Normal };
        var requests = new List<BloodRequest>();
        for (var i = 0; i < 12; i++)
        {
            var area = Areas[(i * 3 + 1) % Areas.Length];
            var owner = users[(i * 5 + 2) % users.Count];
            var status = i switch
            {
                9 or 10 => RequestStatus.Fulfilled,
                11 => RequestStatus.Cancelled,
                _ => RequestStatus.Open
            };

            requests.Add(new BloodRequest
            {
                RequesterId = owner.Id,
                PatientName = "Demo Patient " + (i + 1),
                BloodType = allTypes[rnd.Next(allTypes.Length)],
                UnitsNeeded = status == RequestStatus.Fulfilled ? 1 : rnd.Next(1, 4),
                Hospital = Hospitals[i % Hospitals.Length],
                City = "Cairo",
                Latitude = area.Lat + (rnd.NextDouble() - 0.5) * 0.02,
                Longitude = area.Lng + (rnd.NextDouble() - 0.5) * 0.02,
                Urgency = urgencies[i % urgencies.Length],
                Status = status,
                ContactPhone = $"+2011000{i + 1:D5}",
                Notes = i % 3 == 0 ? "Demo request - not a real patient." : null,
                CreatedAt = now.AddHours(-rnd.Next(1, 72)),
                NeededBy = status == RequestStatus.Open ? now.AddHours(rnd.Next(6, 96)) : null
            });
        }
        db.BloodRequests.AddRange(requests);
        await db.SaveChangesAsync();

        // ---- pledges (always from a compatible donor who is not the requester) ----
        var pledges = new List<Pledge>();
        foreach (var r in requests)
        {
            if (r.Status == RequestStatus.Cancelled) continue;

            var candidates = users
                .Where(u => u.Id != r.RequesterId
                            && u.BloodType != null
                            && BloodCompatibility.CanDonate(u.BloodType.Value, r.BloodType)
                            && u.IsAvailableToDonate)
                .ToList();
            if (candidates.Count == 0) continue;

            var donor = candidates[rnd.Next(candidates.Count)];
            if (r.Status == RequestStatus.Fulfilled)
            {
                pledges.Add(new Pledge { RequestId = r.Id, DonorId = donor.Id, Status = PledgeStatus.Donated, CreatedAt = r.CreatedAt.AddHours(2) });
                donor.LastDonationDate = now.AddDays(-rnd.Next(2, 20));
            }
            else if (rnd.Next(3) == 0)
            {
                pledges.Add(new Pledge { RequestId = r.Id, DonorId = donor.Id, Status = PledgeStatus.Pledged, CreatedAt = now.AddMinutes(-rnd.Next(5, 600)) });
            }
        }
        db.Pledges.AddRange(pledges);
        await db.SaveChangesAsync();
    }
}
