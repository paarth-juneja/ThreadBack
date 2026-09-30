using System.IO;
using System.Text.Json;

namespace ThreadBack.App;

public sealed class ModelSettings
{
    public int VisionIdleMinutes { get; set; } = 0;
    public int TextIdleMinutes { get; set; } = -1;
    public int MemoryLimitGb { get; set; } = 0;
    public bool GpuEnabled { get; set; }
    public bool NpuEnabled { get; set; }

    public static bool ValidIdle(int value) => value is -1 or 0 or 2 or 5;
    public static bool ValidMemory(int value) => value is 0 or 6 or 8 or 10;
}

public sealed class ModelSettingsStore
{
    private readonly string path;
    public ModelSettingsStore(string? path = null) => this.path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreadBack", "settings.json");
    public ModelSettings Load()
    {
        try
        {
            if (!File.Exists(path)) return new();
            var settings = JsonSerializer.Deserialize<ModelSettings>(File.ReadAllText(path)) ?? new();
            if (!ModelSettings.ValidIdle(settings.TextIdleMinutes) || !ModelSettings.ValidIdle(settings.VisionIdleMinutes) || !ModelSettings.ValidMemory(settings.MemoryLimitGb) || settings.GpuEnabled && settings.NpuEnabled) return new();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(ModelSettings settings)
    {
        if (!ModelSettings.ValidIdle(settings.TextIdleMinutes) || !ModelSettings.ValidIdle(settings.VisionIdleMinutes) || !ModelSettings.ValidMemory(settings.MemoryLimitGb) || settings.GpuEnabled && settings.NpuEnabled) throw new InvalidDataException("Invalid model settings.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
        File.Move(temporary, path, true);
    }
}
