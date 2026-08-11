using SpaceGame.Config;

namespace SpaceGame;

public class SpaceGame
{
    public static SpaceGame? Instance;

    public GameFolder GameFolder { get; private set; }
    public GameSettings? Settings { get; private set; }

    public SpaceGame()
    {
        GameFolder = new GameFolder();
        Settings = GameFolder.CreateSettings();

        Loop();
        Exit();
    }

    private void Loop()
    {

    }

    private void Exit()
    {
        Settings!.Save(GameFolder.SettingsPath!);
    }

    static void Main(string[] args)
    {
        Instance = new SpaceGame();
    }
}
