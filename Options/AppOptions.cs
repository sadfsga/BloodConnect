namespace BloodConnect.Api.Options;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "BloodConnect";
    public string Audience { get; set; } = "BloodConnectClients";
    public int ExpiryMinutes { get; set; } = 120;
}

public class MatchingOptions
{
    // How far (km) from a request we look for donors.
    public double RadiusKm { get; set; } = 25;

    // Minimum days between two whole-blood donations.
    // Check the rule with your local blood bank before using this in real life.
    public int MinDaysBetweenDonations { get; set; } = 56;
}
