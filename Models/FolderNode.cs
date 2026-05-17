using System.Collections.ObjectModel;

namespace WPFToolbarTree.Models;

public sealed class FolderNode : TreeNode
{
    public ObservableCollection<TreeNode> Children { get; } = new();
}
