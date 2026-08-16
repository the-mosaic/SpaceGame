using Serilog;
using Serilog.Core;
using SpaceGame.Config;

namespace SpaceGame;

public class SpaceGame
{
    public static SpaceGame? Instance { get; private set; }
    public static Logger? Logger { get; private set; }

    public GameFolder GameFolder { get; private set; }
    public GameSettings? Settings { get; private set; }

    private readonly ILogger _log;

    public SpaceGame()
    {
        Logger = CreateLogger();
        _log = Logger.ForContext<SpaceGame>();
        _log.Information("Starting up!");

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
        _log.Information("Exiting!");
        Settings!.Save(GameFolder.SettingsPath!);
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    static void Main(string[] args)
    {
        Instance = new SpaceGame();
    }
}
