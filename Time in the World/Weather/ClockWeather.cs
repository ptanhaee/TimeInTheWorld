using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Time_in_the_World;

public sealed class ClockWeather : INotifyPropertyChanged, IDisposable
{
    private CancellationTokenSource? _request;
    private DateTimeOffset _nextRefresh;
    private bool _disposed;
    public WeatherLocation? Location { get; private set; }
    public WeatherReport? Report { get; private set; }
    public bool HasWeather => Report is not null;
    public bool HasLocation => Location is not null;
    public string Status { get; private set; } = "";
    public bool HasStatus => HasLocation && Status.Length > 0;
    public string CityActionLabel => Location is null ? "＋  Choose weather city" : "Edit weather city";
    public string CityDescription => Location?.DisplayName ?? "Choose a city to see its weather here.";

    public void SetLocation(WeatherLocation? location)
    {
        var validLocation = location?.IsValid == true ? location : null;
        if (Location == validLocation) return;
        CancelRequest();
        Location = validLocation;
        Report = null;
        Status = Location is null ? "" : "Waiting for weather…";
        _nextRefresh = DateTimeOffset.MinValue;
        Notify();
    }

    public async Task RefreshAsync(WeatherService service, DateTimeOffset now, bool force = false)
    {
        if (_disposed || Location is null || _request is not null || (!force && now < _nextRefresh)) return;
        var request = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _request = request;
        var city = Location;
        Status = Report is null ? "Loading weather…" : "";
        Notify();
        try
        {
            var report = await service.GetWeatherAsync(city, now, request.Token);
            if (_disposed || request.IsCancellationRequested || _request != request) return;
            Report = report;
            Status = "";
            _nextRefresh = now.AddMinutes(30);
            Notify();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            // A failed request only hides this card's weather. Other cards keep their data.
            if (_disposed || _request != request) return;
            Report = null;
            Status = ex switch
            {
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "Weather service busy · retry at next update",
                OperationCanceledException => "Weather request timed out · retry at next update",
                JsonException => "Weather data unavailable · retry at next update",
                _ => "Weather unavailable · retry at next update"
            };
            _nextRefresh = now.AddMinutes(30);
            Notify();
        }
        finally
        {
            if (_request == request) _request = null;
            request.Dispose();
        }
    }

    private void CancelRequest()
    {
        var request = _request;
        _request = null;
        request?.Cancel();
        // The request's own finally block disposes its source after the await completes.
    }

    public void Dispose()
    {
        _disposed = true;
        CancelRequest();
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    public event PropertyChangedEventHandler? PropertyChanged;
}
