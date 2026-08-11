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

    public GameFolder()
    {
        var homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        GameFolderPath = Path.Combine(homeFolder, Files.SpaceFolder);

        if (!Directory.Exists(GameFolderPath))
            Directory.CreateDirectory(GameFolderPath);
    }

    public GameSettings? CreateSettings()
    {
        SettingsPath = Path.Combine(GameFolderPath!, Files.Settings);

        if (File.Exists(SettingsPath))
            return GameSettings.Load(SettingsPath);

        return new(SettingsPath);
    }
}