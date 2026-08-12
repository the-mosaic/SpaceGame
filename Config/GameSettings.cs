using System.Text.Json;

namespace SpaceGame.Config;

public struct WindowSize(int width, int height)
{
    public int Width = width;
    public int Height = height;
};

public class GameSettings
{
    public WindowSize WindowSize { get; set; } = new(800, 600);
    public bool Fullscreen { get; set; } = false;
    public string LanguageCode { get; set; } = "en";
    public bool DebugMode { get; private set; } = false;

    public GameSettings() { }

    public GameSettings(string filePath)
    {
        Save(filePath);
    }

    public static GameSettings Load(string filePath)
    {
        var settingsText = File.ReadAllText(filePath);
        var result = JsonSerializer.Deserialize<GameSettings>(settingsText);
        return result!;
    }

    public void Save(string filePath)
    {
        var settingsText = JsonSerializer.Serialize(this);
        File.WriteAllText(filePath, settingsText);
    }
}
