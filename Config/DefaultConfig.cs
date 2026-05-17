using System.IO;

namespace WPFToolbarTree.Config;

public static class DefaultConfig
{
    public const string Json = """
        {
          "items": [
            {
              "type": "folder",
              "name": "Examples",
              "children": [
                { "type": "item", "name": "Notepad",   "path": "%WINDIR%\\System32\\notepad.exe" },
                { "type": "item", "name": "Calculator","path": "%WINDIR%\\System32\\calc.exe" },
                { "type": "item", "name": "GitHub",    "path": "https://github.com" }
              ]
            },
            { "type": "item", "name": "Downloads", "path": "%USERPROFILE%\\Downloads" }
          ]
        }
        """;

    public static void WriteIfMissing()
    {
        AppPaths.EnsureDataDir();
        if (!File.Exists(AppPaths.ConfigFile))
            File.WriteAllText(AppPaths.ConfigFile, Json);
    }
}
