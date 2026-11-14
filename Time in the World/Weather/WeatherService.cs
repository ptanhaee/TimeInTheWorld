using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Time_in_the_World;

public sealed class WeatherService(HttpClient client)
{
    // A shared client is supplied by the window; no connections are opened on the UI thread.
    public async Task<IReadOnlyList<WeatherLocation>> SearchCitiesAsync(string query, CancellationToken cancellationToken)
    {
        if (query.Trim().Length < 2) return Array.Empty<WeatherLocation>();
        var url = "https://geocoding-api.open-meteo.com/v1/search?count=30&language=en&format=json&name="
                  + Uri.EscapeDataString(query.Trim());
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid city response.");
        if (!root.TryGetProperty("results", out var results)) return Array.Empty<WeatherLocation>();
        if (results.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid city results.");
        var cities = new List<WeatherLocation>();
        foreach (var item in results.EnumerateArray())
        {
            var city = new WeatherLocation(Text(item, "name"), OptionalText(item, "admin1"), OptionalText(item, "country"),
                Number(item, "latitude"), Number(item, "longitude"), OptionalText(item, "timezone"));
            if (city.IsValid) cities.Add(city);
        }
        return cities;
    }

    public async Task<WeatherReport> GetWeatherAsync(WeatherLocation city, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!city.IsValid) throw new ArgumentException("Invalid weather city.", nameof(city));
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={city.Latitude}&longitude={city.Longitude}")
                  + "&current=temperature_2m,weather_code,is_day&hourly=temperature_2m,weather_code"
                  + "&daily=weather_code,temperature_2m_max,temperature_2m_min"
                  // timezone=auto aligns hourly data and daily highs/lows to the city's local days.
                  + "&temperature_unit=celsius&timeformat=unixtime&timezone=auto&forecast_days=8";
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseForecast(json, city, now);
    }

    public static WeatherReport ParseForecast(string json, WeatherLocation city, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var current = Property(root, "current");
        var zone = city.GetTimeZone();
        var currentTime = Timestamp(Property(current, "time"));
        // Never label a stale or implausibly future response as current weather.
        if (currentTime < now.AddHours(-2) || currentTime > now.AddHours(1))
            throw new JsonException("Current weather is out of date.");
        var currentReading = Reading(currentTime, Number(current, "temperature_2m"),
            Integer(Property(current, "weather_code")), Integer(Property(current, "is_day")), zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date);

        // Today's periods in city-local time, matching the clock's daytime definition (06:00–17:59).
        var hourly = Property(root, "hourly");
        var times = ReadArray(hourly, "time");
        var temperatures = ReadArray(hourly, "temperature_2m");
        var codes = ReadArray(hourly, "weather_code");
        if (times.GetArrayLength() == 0 || new[] { temperatures, codes }.Any(a => a.GetArrayLength() != times.GetArrayLength()))
            throw new JsonException("Incomplete hourly forecast.");
        List<int> morning = [], noon = [], night = [];
        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            var local = TimeZoneInfo.ConvertTime(Timestamp(times[i]), zone);
            var date = DateOnly.FromDateTime(local.Date);
            if (date == today && local.Hour is >= 6 and < 12) morning.Add(i);
            else if (date == today && local.Hour is >= 12 and < 18) noon.Add(i);
            else if (date == today && local.Hour >= 18 || date == today.AddDays(1) && local.Hour < 6) night.Add(i);
        }

        WeatherPeriod Period(string name, string hours, List<int> indexes, int expectedHours, bool isDay)
        {
            // Allow one hour of slack for a daylight-saving change; otherwise an average would hide missing data.
            if (indexes.Count < expectedHours - 1) throw new JsonException($"Incomplete {name.ToLowerInvariant()} forecast.");
            var average = indexes.Average(i => Number(temperatures[i]));
            // Most frequent condition; ties go to the higher (more significant) WMO code.
            var code = indexes.Select(i => Integer(codes[i])).GroupBy(c => c)
                .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            return new WeatherPeriod(name, hours, average, code, isDay, indexes.Count);
        }

        // The next seven city-local days after today.
        var daily = Property(root, "daily");
        var days = ReadArray(daily, "time");
        var highs = ReadArray(daily, "temperature_2m_max");
        var lows = ReadArray(daily, "temperature_2m_min");
        var dailyCodes = ReadArray(daily, "weather_code");
        if (new[] { highs, lows, dailyCodes }.Any(a => a.GetArrayLength() != days.GetArrayLength()))
            throw new JsonException("Incomplete daily forecast.");
        var nextDays = new DailyForecast?[7];
        for (var i = 0; i < days.GetArrayLength(); i++)
        {
            // Daily timestamps mark local midnight; using midday avoids off-by-one dates at day boundaries.
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Timestamp(days[i]).AddHours(12), zone).Date);
            var offset = date.DayNumber - today.DayNumber;
            if (offset is >= 1 and <= 7 && nextDays[offset - 1] is null)
                nextDays[offset - 1] = new DailyForecast(date, Number(highs[i]), Number(lows[i]), Integer(dailyCodes[i]));
        }
        if (nextDays.Any(d => d is null)) throw new JsonException("Incomplete 7-day forecast.");

        return new WeatherReport(currentReading,
            Period("Morning", "6 AM – 12 PM", morning, 6, true),
            Period("Noon", "12 PM – 6 PM", noon, 6, true),
            Period("Night", "6 PM – 6 AM", night, 12, false),
            nextDays.Select(d => d!).ToArray(), now);
    }

    private static WeatherReading Reading(DateTimeOffset time, double temperature, int code, int day, TimeZoneInfo zone) =>
        new(time, temperature, code, day == 1,
            TimeZoneInfo.ConvertTime(time, zone).ToString("ddd h:mm tt", CultureInfo.InvariantCulture));

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value : throw new JsonException($"Missing {name}.");
    private static JsonElement ReadArray(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Array ? value : throw new JsonException($"Invalid {name}.");
    }
    private static string Text(JsonElement element, string name) =>
        Property(element, name).ValueKind == JsonValueKind.String
            ? Property(element, name).GetString()! : throw new JsonException($"Invalid {name}.");
    private static string OptionalText(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static double Number(JsonElement element, string name) => Number(Property(element, name));
    private static double Number(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number) && double.IsFinite(number)
            ? number : throw new JsonException("Invalid weather number.");
    private static int Integer(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)
            ? number : throw new JsonException("Invalid weather code.");
    private static DateTimeOffset Timestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var number)
            || number is < -62135596800 or > 253402300799) throw new JsonException("Invalid forecast time.");
        return DateTimeOffset.FromUnixTimeSeconds(number);
    }
}
