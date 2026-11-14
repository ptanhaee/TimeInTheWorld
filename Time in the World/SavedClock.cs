namespace Time_in_the_World;

// The optional city keeps settings from earlier versions compatible.
public sealed record SavedClock(string Name, string TimeZoneId, WeatherLocation? WeatherCity = null)
{
    public static SavedClock FromModel(ClockModel clock) => new(clock.Name, clock.Zone.Id, clock.Weather.Location);

    public ClockModel ToModel()
    {
        var clock = new ClockModel(Name, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
        clock.Weather.SetLocation(WeatherCity);
        return clock;
    }
}
