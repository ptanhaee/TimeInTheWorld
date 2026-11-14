using System.ComponentModel;
using System.Globalization;

namespace Time_in_the_World;

public sealed class ClockModel : INotifyPropertyChanged
{
    public string Name { get; }
    public TimeZoneInfo Zone { get; }
    public ClockWeather Weather { get; } = new();
    public DateTimeOffset Time { get; private set; }
    public string DigitalTime => Time.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture);
    public string Date => Time.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
    public string Offset => $"UTC{(Time.Offset < TimeSpan.Zero ? "−" : "+")}{Time.Offset.Duration():hh\\:mm}";
    public string DayRelation { get; private set; } = "";
    public string Period => Time.Hour is >= 6 and < 18 ? "DAYTIME" : "NIGHTTIME";
    public string ZoneName => Zone.DisplayName;

    public ClockModel(string name, TimeZoneInfo zone)
    {
        Name = name;
        Zone = zone;
        Update(DateTimeOffset.UtcNow);
    }

    public void Update(DateTimeOffset utcNow)
    {
        Time = TimeZoneInfo.ConvertTime(utcNow, Zone);
        var dayDifference = (Time.Date - utcNow.ToLocalTime().Date).Days;
        DayRelation = dayDifference switch
        {
            -1 => "Yesterday where you are",
            0 => "Same day as you",
            1 => "Tomorrow where you are",
            _ => $"{dayDifference:+0;-0} days from you"
        };
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
