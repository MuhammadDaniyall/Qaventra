namespace HRMS.Api.Services;

public static class TimeZones
{
    /// <summary>
    /// Resolves an IANA id such as "Asia/Karachi" on any host, also Windows servers without ICU,
    /// by trying the Windows id too. Falls back to UTC only when neither exists.
    /// </summary>
    public static TimeZoneInfo Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TimeZoneInfo.Utc;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone))
            return zone;
        return id == "Asia/Karachi" && TimeZoneInfo.TryFindSystemTimeZoneById("Pakistan Standard Time", out zone) ? zone : TimeZoneInfo.Utc;
    }
}
