using Serilog;
using Serilog.Core;
using SpaceGame.Config;
using Veldrid.MetalBindings;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vulkan.Xlib;

namespace SpaceGame;

public class SpaceGame
{
    public static SpaceGame? Instance { get; private set; }
    public static Logger? Logger { get; private set; }

    public GameFolder GameFolder { get; private set; }
    public GameSettings? Settings { get; private set; }
    public Sdl2Window Window { get; private set; }

    private readonly ILogger _log;

    public SpaceGame()
    {
        Logger = CreateLogger();
        _log = Logger.ForContext<SpaceGame>();
        _log.Information("Starting up!");

        GameFolder = new GameFolder();
        Settings = GameFolder.CreateSettings();
        Window = CreateWindow(Settings.WindowSize.Width, Settings.WindowSize.Height, "Space game");

        Loop();
        Exit();
    }

    private void Loop()
    {
        while (Window.Exists)
        {
            Window.PumpEvents();
        }
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

    private static Sdl2Window CreateWindow(int width, int height, string title)
    {
        var createInfo = new WindowCreateInfo()
        {
            X = 100,
            Y = 100,
            WindowWidth = width,
            WindowHeight = height,
            WindowTitle = title
        };

        return VeldridStartup.CreateWindow(ref createInfo);
    }

    static void Main(string[] args)
    {
        Instance = new SpaceGame();
    }
}
