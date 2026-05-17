using System.IO;
using System.Text.Json;
using WPFToolbarTree.Models;

namespace WPFToolbarTree.Config;

public static class ConfigSerializer
{
    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder
            .UnsafeRelaxedJsonEscaping,
    };

    public static void Save(IEnumerable<TreeNode> items)
    {
        var root = new RootDto { Items = items.Select(ToDto).ToList() };
        AppPaths.EnsureDataDir();
        var tmp = AppPaths.ConfigFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(root, WriteOpts));
        // atomic-ish replace so the FileSystemWatcher sees one settled file
        File.Move(tmp, AppPaths.ConfigFile, overwrite: true);
    }

    private static NodeDto ToDto(TreeNode node) => node switch
    {
        FolderNode f => new NodeDto
        {
            Type = "folder",
            Name = f.Name,
            Children = f.Children.Select(ToDto).ToList(),
        },
        ItemNode i => new NodeDto
        {
            Type = "item",
            Name = i.Name,
            Path = string.IsNullOrEmpty(i.RawPath) ? i.Path : i.RawPath,
        },
        _ => throw new InvalidOperationException($"Unknown node {node.GetType().Name}"),
    };
}
