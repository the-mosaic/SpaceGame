namespace SpaceGame.Engine.Render;

public class RenderUtil
{
    public static string VersionToString(uint version)
    {
        uint major = version >> 22;
        uint minor = (version >> 12) & 0x3FF;
        uint patch = version & 0xFFF;

        return $"{major}.{minor}.{patch}";
    }
}