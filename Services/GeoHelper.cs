namespace BloodConnect.Api.Services;

public static class GeoHelper
{
    private const double EarthRadiusKm = 6371.0;

    // Haversine formula: straight-line distance between two GPS points.
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    // A cheap rectangle around a point. We use it in SQL to throw away far-away rows
    // before doing the exact distance maths in memory.
    public static (double MinLat, double MaxLat, double MinLng, double MaxLng) BoundingBox(
        double lat, double lng, double radiusKm)
    {
        var dLat = radiusKm / 111.0;
        var dLng = radiusKm / (111.0 * Math.Max(0.01, Math.Cos(ToRad(lat))));
        return (lat - dLat, lat + dLat, lng - dLng, lng + dLng);
    }

    private static double ToRad(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
