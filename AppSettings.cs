using System.Text.Json;

namespace CursorDodge;

internal sealed class AppSettings
{
    public int DistancePx { get; set; } = 160;
    public double AngleDegrees { get; set; } = 315.0;
    public int FrameRate { get; set; } = 120;
    public int MoveDurationMs { get; set; } = 200;
    public int ArmTimeoutMs { get; set; } = 500;

    private const int DistanceMin = 10;
    private const int DistanceMax = 3000;
    private const int FrameRateMin = 10;
    private const int FrameRateMax = 240;
    private const int DurationMin = 30;
    private const int DurationMax = 3000;
    private const int ArmTimeoutMin = 50;
    private const int ArmTimeoutMax = 3000;

    public void Normalize()
    {
        DistancePx = Math.Clamp(DistancePx, DistanceMin, DistanceMax);
        AngleDegrees = ((AngleDegrees % 360.0) + 360.0) % 360.0;
        FrameRate = Math.Clamp(FrameRate, FrameRateMin, FrameRateMax);
        MoveDurationMs = Math.Clamp(MoveDurationMs, DurationMin, DurationMax);
        ArmTimeoutMs = Math.Clamp(ArmTimeoutMs, ArmTimeoutMin, ArmTimeoutMax);
    }

    public static string GetConfigPath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CursorDodge");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings.json");
    }

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
            return new AppSettings();

        try
        {
            using var stream = File.OpenRead(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(stream);
            if (loaded is null)
                return new AppSettings();

            loaded.Normalize();
            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        Normalize();
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
    }
}
