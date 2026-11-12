using Coordinate = (double Latitude, double Longitude);
using Route = System.Collections.Generic.List<(double Latitude,
                                                double Longitude)>;

namespace MasteringModernCSharp.Ch03;

public static class Geo
{
    private const double EarthRadiusKm = 6371;

    public static double DistanceKm(Coordinate from, Coordinate to)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180;

        var dLat = Rad(to.Latitude - from.Latitude);
        var dLon = Rad(to.Longitude - from.Longitude);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Rad(from.Latitude)) * Math.Cos(Rad(to.Latitude)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    public static double LengthKm(Route route)
    {
        var total = 0.0;
        for (var i = 1; i < route.Count; i++)
            total += DistanceKm(route[i - 1], route[i]);
        return total;
    }
}
