using System.Text.Json;

namespace IllustratorTypeFlow;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;
    private readonly FileLogger logger;

    public SettingsStore(string dataDirectory, FileLogger logger)
    {
        Directory.CreateDirectory(dataDirectory);
        path = Path.Combine(dataDirectory, "settings.json");
        this.logger = logger;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                   ?? new AppSettings();
        }
        catch (Exception exception)
        {
            logger.Error("读取设置失败，使用默认设置", exception);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception)
        {
            logger.Error("保存设置失败", exception);
        }
    }
}

