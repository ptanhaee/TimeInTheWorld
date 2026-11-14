using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Time_in_the_World;

public partial class WeatherCityWindow : Window
{
    private readonly WeatherService _service;
    private CancellationTokenSource? _search;
    private bool _closed;
    public WeatherLocation? SelectedCity { get; private set; }

    public WeatherCityWindow(ClockModel clock, WeatherService service)
    {
        _service = service;
        InitializeComponent();
        ClockDescription.Text = $"Choose the weather city for {clock.Name}.";
        SavedCity.Text = clock.Weather.Location is { } city ? $"Saved city: {city.DisplayName}" : "No weather city selected yet.";
        RemoveButton.Visibility = clock.Weather.Location is null ? Visibility.Collapsed : Visibility.Visible;
        SearchBox.Text = clock.Weather.Location?.Name ?? clock.Name;
        if (clock.Weather.Location is { } selectedCity)
        {
            // Restore the actual saved result without requiring another online search.
            CityList.ItemsSource = new[] { selectedCity };
            CityList.SelectedItem = selectedCity;
            SearchStatus.Text = "Your saved city is selected. Search to choose a different city.";
        }
        Loaded += (_, _) => { SearchBox.Focus(); SearchBox.SelectAll(); };
        Closed += (_, _) => { _closed = true; _search?.Cancel(); };
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SearchAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search?.Cancel();
        _search = null;
        if (CityList is null) return;
        CityList.ItemsSource = null;
        SearchButton.IsEnabled = true;
        SearchStatus.Text = "Search a city, then select the matching region and country.";
    }

    private async Task SearchAsync()
    {
        _search?.Cancel();
        var query = SearchBox.Text.Trim();
        CityList.ItemsSource = null;
        if (query.Length < 2) { SearchStatus.Text = "Enter at least two letters of a city name."; return; }
        var request = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _search = request;
        SearchButton.IsEnabled = false;
        SearchStatus.Text = "Searching cities…";
        try
        {
            var cities = await _service.SearchCitiesAsync(query, request.Token);
            if (_closed || _search != request || request.IsCancellationRequested) return;
            CityList.ItemsSource = cities;
            SearchStatus.Text = cities.Count == 0 ? "No cities found. Try a nearby city or a different spelling." : "Select the city, then choose Save city.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            if (!_closed && _search == request)
                SearchStatus.Text = "City search is unavailable. Check your connection and try again. Your saved city is unchanged.";
        }
        finally
        {
            if (!_closed && _search == request) { SearchButton.IsEnabled = true; _search = null; }
            request.Dispose();
        }
    }

    private void CityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SaveButton is not null) SaveButton.IsEnabled = CityList.SelectedItem is WeatherLocation;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (CityList.SelectedItem is not WeatherLocation city) return;
        SelectedCity = city;
        DialogResult = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e) { SelectedCity = null; DialogResult = true; }
}
