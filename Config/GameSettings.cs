using System.Text.Json;

namespace SpaceGame.Config;

public class GameSettings
{
    public int ScreenWidth { get; private set; } = 800;
    public int ScreenHeight { get; private set; } = 600;
    public bool Fullscreen { get; private set; } = false;
    public string LanguageCode { get; private set; } = "en";
    public bool DebugMode { get; private set; } = false;

    public GameSettings(string filePath)
    {
        Save(filePath);
    }

    public static GameSettings? Load(string filePath)
    {
        var settingsText = File.ReadAllText(filePath);
        var result = JsonSerializer.Deserialize<GameSettings>(settingsText);
        return result;
    }

    public void Save(string filePath)
    {
        var settingsText = JsonSerializer.Serialize(this);
        File.WriteAllText(filePath, settingsText);
    }
}
