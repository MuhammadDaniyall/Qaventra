namespace HRMS.Domain.Services;

public static class WorkCalendar
{
    /// <summary>Working days in the inclusive range, skipping weekly off days and holidays.</summary>
    public static int CountWorkingDays(DateOnly from, DateOnly to, IReadOnlyCollection<DayOfWeek> weeklyOff, IReadOnlySet<DateOnly> holidays)
    {
        if (to < from)
            return 0;

        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!weeklyOff.Contains(d.DayOfWeek) && !holidays.Contains(d))
                count++;
        }
        return count;
    }

    /// <summary>Overlap of two inclusive date ranges, or null when they do not overlap.</summary>
    public static (DateOnly From, DateOnly To)? Overlap(DateOnly aFrom, DateOnly aTo, DateOnly bFrom, DateOnly bTo)
    {
        var from = aFrom > bFrom ? aFrom : bFrom;
        var to = aTo < bTo ? aTo : bTo;
        return from <= to ? (from, to) : null;
    }
}

public static class GeoFence
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Great-circle distance in meters (haversine).</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
