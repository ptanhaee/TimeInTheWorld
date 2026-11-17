using System.Globalization;

namespace Time_in_the_World;

internal static class WeatherCodes
{
    public static string Icon(int code, bool isDay) => code switch
    {
        0 or 1 => isDay ? "☀" : "☽",
        2 => isDay ? "⛅" : "☁",
        3 => "☁",
        45 or 48 => "≋",
        >= 51 and <= 67 or >= 80 and <= 82 => "☂",
        >= 71 and <= 77 or 85 or 86 => "❄",
        >= 95 and <= 99 => "ϟ",
        _ => "○"
    };

    public static string Description(int code, bool isDay) => code switch
    {
        0 => isDay ? "Clear sky" : "Clear night",
        1 => "Mostly clear", 2 => "Partly cloudy", 3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle", 56 or 57 => "Freezing drizzle",
        61 => "Light rain", 63 => "Rain", 65 => "Heavy rain",
        66 or 67 => "Freezing rain",
        71 => "Light snow", 73 => "Snow", 75 => "Heavy snow", 77 => "Snow grains",
        80 or 81 or 82 => "Rain showers", 85 or 86 => "Snow showers",
        95 or 97 => "Thunderstorms", 96 or 99 => "Thunderstorms / hail",
        _ => "Conditions unavailable"
    };

    public static string Degrees(double celsius) => $"{celsius.ToString("0", CultureInfo.CurrentCulture)}°";
}

public sealed record WeatherReading(DateTimeOffset Time, double Celsius, int Code, bool IsDay, string LocalTime)
{
    public string Temperature => $"{WeatherCodes.Degrees(Celsius)}C";
    public string Icon => WeatherCodes.Icon(Code, IsDay);
    public string Description => WeatherCodes.Description(Code, IsDay);
}

/// <summary>Average temperature and most common conditions over part of the city's current day.</summary>
public sealed record WeatherPeriod(string Name, string Hours, double Celsius, int Code, bool IsDay, int HourCount)
{
    public string Temperature => $"{WeatherCodes.Degrees(Celsius)}C";
    public string Icon => WeatherCodes.Icon(Code, IsDay);
    public string Description => WeatherCodes.Description(Code, IsDay);
    public string ToolTip => $"{Name}: average of {HourCount} hourly forecasts, {Hours} city time";
}

public sealed record DailyForecast(DateOnly Date, double MaxCelsius, double MinCelsius, int Code)
{
    public string DayName => Date.ToString("ddd", CultureInfo.InvariantCulture);
    public string DateLabel => Date.ToString("MMM d", CultureInfo.InvariantCulture);
    public string High => WeatherCodes.Degrees(MaxCelsius);
    public string Low => WeatherCodes.Degrees(MinCelsius);
    public string Icon => WeatherCodes.Icon(Code, true);
    public string Description => WeatherCodes.Description(Code, true);
    public string ToolTip => $"{Date.ToString("dddd, MMM d", CultureInfo.InvariantCulture)}: {Description}, high {High}C, low {Low}C";
}

public sealed record WeeklyForecast(string Name, DateOnly StartDate, DateOnly EndDate,
    double? DayCelsius, double? NightCelsius)
{
    public string DateLabel => $"{StartDate.ToString("MMM d", CultureInfo.InvariantCulture)} – {EndDate.ToString("MMM d", CultureInfo.InvariantCulture)}";
    public string DayTemperature => DayCelsius is { } value ? $"{WeatherCodes.Degrees(value)}C" : "Unavailable";
    public string NightTemperature => NightCelsius is { } value ? $"{WeatherCodes.Degrees(value)}C" : "Unavailable";
    public string ToolTip => $"{DateLabel}: averages of hourly temperatures in city time. Day: 6 AM–6 PM. Night: 6 PM–6 AM the following day. Incomplete periods are unavailable.";
}

public sealed record WeatherReport(WeatherReading Current, WeatherPeriod Morning, WeatherPeriod Noon,
    WeatherPeriod Night, IReadOnlyList<DailyForecast> NextDays, IReadOnlyList<WeeklyForecast> NextWeeks,
    DateTimeOffset FetchedAt)
{
    public string UpdatedLabel => $"Updated {FetchedAt.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture)} · your time";
}
