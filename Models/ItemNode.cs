namespace WPFToolbarTree.Models;

public sealed class ItemNode : TreeNode
{
    public string Path { get; init; } = string.Empty;
    public string RawPath { get; init; } = string.Empty;
    public EntryKind Kind { get; init; }
}
