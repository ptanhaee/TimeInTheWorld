namespace Time_in_the_World;

public sealed class WeatherRefreshCoordinator(WeatherService service)
{
    public Task RefreshAsync(IEnumerable<ClockWeather> clocks, DateTimeOffset now)
    {
        // The real forecast request is the connectivity check. A separate HEAD probe
        // can fail even while forecast GETs work, and must never gate or clear other cards.
        // Each card owns its own timeout, retry schedule, and in-flight cancellation.
        return Task.WhenAll(clocks.Select(clock => clock.RefreshAsync(service, now)).ToArray());
    }
}
