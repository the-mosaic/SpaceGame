using Serilog;
using Serilog.Core;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SpaceGame.Config;
using SpaceGame.Engine.Render;

namespace SpaceGame;

public class SpaceGame
{
    public static readonly Version32 ApplicationVersion = new(0, 0, 1);

    public static SpaceGame? Instance { get; private set; }
    public static Logger? Logger { get; private set; }

    public GameFolder GameFolder { get; private set; }
    public GameSettings? Settings { get; private set; }

    // grahics-related variables
    public IWindow SpWindow { get; private set; }
    public Renderer Renderer { get; private set; }

    private readonly ILogger _log;

    public SpaceGame()
    {
        Instance = this;

        Logger = CreateLogger();
        _log = Logger.ForContext<SpaceGame>();
        _log.Information("Starting up!");

        GameFolder = new GameFolder();
        Settings = GameFolder.CreateSettings();

        if (Settings.DebugMode)
            _log.Information("Debug mode is ON");

        SpWindow = CreateWindow(Settings.WindowSize);
        Renderer = new Renderer(Settings.DebugMode, SpWindow);

        Loop();
        Exit();
    }

    private static IWindow CreateWindow(WindowSize size)
    {
        var windowOptions = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(size.Width, size.Height),
            Title = "Space game"
        };

        IWindow result = Window.Create(windowOptions);
        result.Initialize();

        if (result.VkSurface is null)
        {
            throw new Exception("Vulkan not supported!");
        }

        return result;
    }

    private void Loop()
    {
        SpWindow.Run();
    }

    private void Exit()
    {
        _log.Information("Exiting!");
        Settings!.Save(GameFolder.SettingsPath!);

        Renderer.Destroy();
        SpWindow.Dispose();
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    static void Main()
    {
        _ = new SpaceGame();
    }
}