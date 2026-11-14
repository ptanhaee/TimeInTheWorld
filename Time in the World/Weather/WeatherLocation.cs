using System.Text.Json.Serialization;

namespace Time_in_the_World;

public sealed record WeatherLocation(string Name, string Region, string Country,
    double Latitude, double Longitude, string TimeZoneId)
{
    [JsonIgnore]
    public string DisplayName => string.Join(", ", new[] { Name, Region, Country }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(TimeZoneId)
        && double.IsFinite(Latitude) && Latitude is >= -90 and <= 90
        && double.IsFinite(Longitude) && Longitude is >= -180 and <= 180;

    public TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return TimeZoneInfo.Utc; }
    }
}
