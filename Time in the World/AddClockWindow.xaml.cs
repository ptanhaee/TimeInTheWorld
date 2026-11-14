using System.Windows;
using System.Windows.Controls;

namespace Time_in_the_World;

public partial class AddClockWindow : Window
{
    private readonly List<ZoneChoice> _zones;
    private string _suggestedName = "";
    public TimeZoneInfo? SelectedZone { get; private set; }
    public string ClockName { get; private set; } = "";

    public AddClockWindow()
    {
        _zones = TimeZoneInfo.GetSystemTimeZones().Select(z => new ZoneChoice(z.StandardName, z)).ToList();
        foreach (var (city, id) in new[]
        {
            ("Montreal", "Eastern Standard Time"), ("Toronto", "Eastern Standard Time"),
            ("New York", "Eastern Standard Time"), ("Vancouver", "Pacific Standard Time"),
            ("Los Angeles", "Pacific Standard Time"), ("London", "GMT Standard Time"),
            ("Canberra", "AUS Eastern Standard Time"), ("Sydney", "AUS Eastern Standard Time"),
            ("Tehran", "Iran Standard Time"), ("Tokyo", "Tokyo Standard Time"),
            ("Paris", "Romance Standard Time"), ("Dubai", "Arabian Standard Time"),
            ("Singapore", "Singapore Standard Time"), ("Auckland", "New Zealand Standard Time"),
            ("New Delhi", "India Standard Time")
        })
        {
            var zone = _zones.FirstOrDefault(z => z.Zone.Id == id)?.Zone;
            if (zone is not null) _zones.Add(new ZoneChoice(city, zone));
        }
        _zones = _zones.OrderBy(z => z.Name).ToList();
        InitializeComponent();
        FilterZones();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterZones();

    private void FilterZones()
    {
        if (ZoneList is null) return;
        var query = SearchBox.Text.Trim();
        var matches = _zones.Where(z => $"{z.Name} {z.Description} {z.Zone.Id}"
            .Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        ZoneList.ItemsSource = matches;
        NoResults.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ZoneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZoneList.SelectedItem is ZoneChoice choice)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == _suggestedName)
                NameBox.Text = choice.Name;
            _suggestedName = choice.Name;
        }
        UpdateAddButton();
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateAddButton();

    private void UpdateAddButton()
    {
        if (AddButton is not null)
            AddButton.IsEnabled = ZoneList.SelectedItem is ZoneChoice && !string.IsNullOrWhiteSpace(NameBox.Text);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (ZoneList.SelectedItem is not ZoneChoice choice || string.IsNullOrWhiteSpace(NameBox.Text)) return;
        SelectedZone = choice.Zone;
        ClockName = NameBox.Text.Trim();
        DialogResult = true;
    }

    private sealed record ZoneChoice(string Name, TimeZoneInfo Zone)
    {
        public string Description => Zone.DisplayName;
    }
}
