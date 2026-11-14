using System.IO;
using System.Text.Json;

namespace Time_in_the_World;

public sealed class ClockSettingsStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Time in the World", "clocks.json");

    public string FilePath { get; }

    public ClockSettingsStore(string? filePath = null) => FilePath = filePath ?? DefaultPath;

    public IReadOnlyList<SavedClock>? Load()
    {
        if (!File.Exists(FilePath)) return null;
        return JsonSerializer.Deserialize<List<SavedClock>>(File.ReadAllText(FilePath))
               ?? throw new JsonException("Missing clock list.");
    }

    public void Save(IEnumerable<SavedClock> clocks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var json = JsonSerializer.Serialize(clocks, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}
