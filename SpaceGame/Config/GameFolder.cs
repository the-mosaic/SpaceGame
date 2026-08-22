using Serilog;

namespace SpaceGame.Config;

public class GameFolder
{
    public class Files
    {
        public const string SpaceFolder = ".space";
        public const string Settings = "settings.json";
    }

    public string? GameFolderPath { get; private set; }
    public string? SettingsPath { get; private set; }

    private readonly ILogger _log;

    public GameFolder()
    {
        _log = SpaceGame.Logger!.ForContext<GameFolder>();

        var homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        GameFolderPath = Path.Combine(homeFolder, Files.SpaceFolder);

        if (!Directory.Exists(GameFolderPath))
        {
            _log.Information("Creating .space folder");
            Directory.CreateDirectory(GameFolderPath);
        }
    }

    public GameSettings CreateSettings()
    {
        SettingsPath = Path.Combine(GameFolderPath!, Files.Settings);

        if (File.Exists(SettingsPath))
        {
            _log.Information("Loading settings");
            return GameSettings.Load(SettingsPath);
        }

        _log.Information("Settings not found, creating");
        return new(SettingsPath);
    }
}