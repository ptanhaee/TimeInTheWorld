using System.Text.Json;
using Time_in_the_World;

// Dependency-free regression checks: dotnet run --project Tests/WeatherForecastChecks
Check("UTC", new DateOnly(2026, 11, 17));
Check("America/Toronto", new DateOnly(2026, 3, 1)); // Spring-forward in week one.
Check("America/Toronto", new DateOnly(2026, 10, 20)); // Fall-back in week two.
Check("Asia/Kathmandu", new DateOnly(2026, 12, 27)); // Fractional offset and year boundary.
Console.WriteLine("Passed: weekly averages, local dates, overnight boundaries, DST, missing data, and request horizon.");

static void Check(string zoneId, DateOnly today)
{
    var city = new WeatherLocation("Test", "", "", 45, -73, zoneId);
    var zone = city.GetTimeZone();
    DateTimeOffset Utc(DateOnly date, int hour = 0) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, 0)), zone));
    var now = Utc(today, 12);
    var hours = new List<long>();
    var temperatures = new List<double?>();
    for (var time = Utc(today); time < Utc(today.AddDays(16)); time = time.AddHours(1))
    {
        var local = TimeZoneInfo.ConvertTime(time, zone);
        var date = DateOnly.FromDateTime(local.Date);
        var periodDate = local.Hour < 6 ? date.AddDays(-1) : date;
        var offset = periodDate.DayNumber - today.DayNumber;
        var isDay = local.Hour is >= 6 and < 18;
        // Distinct values expose inclusion of today, the wrong week, or the wrong half-day.
        temperatures.Add(offset is >= 1 and <= 7 ? (isDay ? 20 : 10)
            : offset is >= 8 and <= 14 ? (isDay ? 30 : 15) : -50);
        hours.Add(time.ToUnixTimeSeconds());
    }
    string Json(int count) => JsonSerializer.Serialize(new
    {
        current = new { time = now.ToUnixTimeSeconds(), temperature_2m = 5, weather_code = 0, is_day = 1 },
        hourly = new
        {
            time = hours.Take(count), temperature_2m = temperatures.Take(count),
            weather_code = Enumerable.Repeat(0, count)
        },
        daily = new
        {
            time = Enumerable.Range(0, 16).Select(d => Utc(today.AddDays(d)).ToUnixTimeSeconds()),
            temperature_2m_max = Enumerable.Repeat(99, 16),
            temperature_2m_min = Enumerable.Repeat(-99, 16), weather_code = Enumerable.Repeat(0, 16)
        }
    });
    var report = WeatherService.ParseForecast(Json(hours.Count), city, now);
    Assert(report.NextDays.Count == 7 && report.NextDays[0].Date == today.AddDays(1), "daily dates");
    Assert(report.NextWeeks.Count == 2, "two weeks");
    var first = report.NextWeeks[0];
    var second = report.NextWeeks[1];
    Assert(first.StartDate == today.AddDays(1) && first.EndDate == today.AddDays(7), "week one dates");
    Assert(second.StartDate == today.AddDays(8) && second.EndDate == today.AddDays(14), "week two dates");
    Assert(first.DayCelsius == 20 && first.NightCelsius == 10, "week one averages");
    Assert(second.DayCelsius == 30 && second.NightCelsius == 15, "week two averages");

    // Missing one temperature must not silently produce a misleading average or hide other weather.
    var missing = hours.IndexOf(Utc(today.AddDays(10), 12).ToUnixTimeSeconds());
    temperatures[missing] = null;
    report = WeatherService.ParseForecast(Json(hours.Count), city, now);
    Assert(report.NextWeeks[1].DayCelsius is null && report.NextWeeks[1].DayTemperature == "Unavailable", "missing temperature");
    Assert(report.NextWeeks[1].NightCelsius == 15 && report.NextWeeks[0].DayCelsius == 20, "independent periods");

    // An eight-day response lacks the morning completing the first week's final night.
    var shortCount = hours.Count(h => h < Utc(today.AddDays(8)).ToUnixTimeSeconds());
    report = WeatherService.ParseForecast(Json(shortCount), city, now);
    Assert(report.NextWeeks[0].DayCelsius == 20 && report.NextWeeks[0].NightCelsius is null, "final overnight boundary");
    Assert(report.NextWeeks[1].DayCelsius is null && report.NextWeeks[1].NightCelsius is null, "short forecast");

    using var client = new HttpClient(new ForecastHandler(Json(hours.Count)));
    new WeatherService(client).GetWeatherAsync(city, now, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"Passed {zoneId}, {today:yyyy-MM-dd}");
}

static void Assert(bool condition, string label)
{
    if (!condition) throw new Exception($"Failed: {label}");
}

sealed class ForecastHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Query.Contains("forecast_days=16") != true)
            throw new Exception("Request must include the final overnight period.");
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
