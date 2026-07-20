using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharsetFlow.Models;

internal sealed class AppSettings
{
    public FilterMode FilterMode { get; set; } = FilterMode.Smart;
    public string IncludeRule { get; set; } = "h hpp c cpp cxx cs java js ts jsx tsx html htm css scss xml json yaml yml md txt ini cfg conf log sql py rb go rs php vue svelte";
    public string ExcludeRule { get; set; } = ".git .vs bin obj node_modules packages";
    public bool Recursive { get; set; } = true;
    public string TargetEncodingId { get; set; } = "utf-8";
    public LineEndingMode LineEnding { get; set; } = LineEndingMode.Preserve;
    public OutputMode OutputMode { get; set; } = OutputMode.InPlace;
    public string OutputDirectory { get; set; } = string.Empty;
    public bool CreateBackup { get; set; }
    public bool VerifyRoundTrip { get; set; } = true;

    [JsonIgnore]
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BeyondXinXin",
        "CharsetFlow",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
