using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace Time_in_the_World;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    // Only checks each card's local due time; network requests happen at most every 30 minutes.
    private readonly DispatcherTimer _weatherTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly WeatherService _weatherService;
    private readonly WeatherRefreshCoordinator _weatherRefresh;
    private bool _closed;
    private readonly ClockSettingsStore _settings = new();
    private string? _storageNotice;
    public ObservableCollection<ClockModel> Clocks { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _weatherService = new WeatherService(_httpClient);
        _weatherRefresh = new WeatherRefreshCoordinator(_weatherService);
        // Stay within the available desktop, including on smaller laptop screens.
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
        DataContext = this;
        LoadClocks();
        UpdateCollectionStatus();
        Tick();
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        _weatherTimer.Start();
        Loaded += async (_, _) => await RefreshWeatherAsync();
        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop();
            _weatherTimer.Stop();
            foreach (var clock in Clocks) clock.Weather.Dispose();
            _httpClient.Dispose();
        };
    }

    private void Tick()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var clock in Clocks) clock.Update(now);
        LocalTimeText.Text = $"Your local time  ·  {now.ToLocalTime():ddd, MMM d}  ·  {now.ToLocalTime().ToString("hh:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private void AddClock_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddClockWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedZone is null) return;
        Clocks.Add(new ClockModel(dialog.ClockName, dialog.SelectedZone));
        SaveClocks();
    }

    private void RemoveClock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClockModel clock })
        {
            clock.Weather.Dispose();
            Clocks.Remove(clock);
            SaveClocks();
        }
    }

    private async void EditWeatherCity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ClockModel clock }) return;
        var dialog = new WeatherCityWindow(clock, _weatherService) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        clock.Weather.SetLocation(dialog.SelectedCity);
        SaveClocks(dialog.SelectedCity is { } city ? $"Weather city saved: {city.DisplayName}." : "Weather city removed and saved.");
        // This request has its own cancellation and never waits on another card's weather.
        await clock.Weather.RefreshAsync(_weatherService, DateTimeOffset.UtcNow, force: true);
    }

    private void WeatherAttribution_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://open-meteo.com/") { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        e.Handled = true;
    }

    private Task RefreshWeatherAsync()
    {
        if (_closed) return Task.CompletedTask;
        return _weatherRefresh.RefreshAsync(Clocks.Select(c => c.Weather), DateTimeOffset.UtcNow);
    }

    private void LoadClocks()
    {
        try
        {
            var saved = _settings.Load();
            if (saved is not null)
            {
                foreach (var item in saved)
                {
                    if (item is null || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.TimeZoneId))
                    {
                        _storageNotice = "Some saved clocks were invalid and could not be loaded.";
                        continue;
                    }
                    try
                    {
                        Clocks.Add(item.ToModel());
                    }
                    catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                    { _storageNotice = "Some saved time zones are unavailable on this computer."; }
                }
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { _storageNotice = "Saved clocks could not be read. Showing the starting clocks."; }
        foreach (var (name, id) in new[]
        {
            ("Montreal", "Eastern Standard Time"), ("Vancouver", "Pacific Standard Time"),
            ("Canberra", "AUS Eastern Standard Time"), ("Iran", "Iran Standard Time"),
            ("England", "GMT Standard Time")
        })
            Clocks.Add(new ClockModel(name, TimeZoneInfo.FindSystemTimeZoneById(id)));
    }

    private void SaveClocks(string? confirmation = null)
    {
        try
        {
            _settings.Save(Clocks.Select(SavedClock.FromModel));
            _storageNotice = confirmation;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _storageNotice = "Your changes work now, but could not be saved for next time."; }
        UpdateCollectionStatus();
    }

    private void UpdateCollectionStatus()
    {
        EmptyState.Visibility = Clocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = _storageNotice ?? $"{Clocks.Count} {(Clocks.Count == 1 ? "clock" : "clocks")}  ·  Updates every second  ·  Daylight saving adjusts automatically";
    }

}
