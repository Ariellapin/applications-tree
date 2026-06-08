namespace WPFToolbarTree.Models;

public sealed class ItemNode : TreeNode
{
    public string Path { get; init; } = string.Empty;
    public string RawPath { get; init; } = string.Empty;
    public EntryKind Kind { get; init; }

    public override string ToolTipText
    {
        get
        {
            var breadcrumb = FullPath;
            if (string.IsNullOrWhiteSpace(Path)) return breadcrumb;
            return breadcrumb + "\n" + Path;
        }
    }
}
