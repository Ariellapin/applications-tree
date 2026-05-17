using System.IO;

namespace WPFToolbarTree.Config;

public static class AppPaths
{
    public static string DataDir { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WPFToolbarTree");

    public static string ConfigFile { get; } = System.IO.Path.Combine(DataDir, "config.json");
    public static string ErrorLog { get; } = System.IO.Path.Combine(DataDir, "error.log");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
