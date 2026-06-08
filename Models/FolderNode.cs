using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WPFToolbarTree.Models;

public sealed class FolderNode : TreeNode
{
    public ObservableCollection<TreeNode> Children { get; }

    public FolderNode()
    {
        Children = new ObservableCollection<TreeNode>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (TreeNode child in e.NewItems)
            {
                child.Parent = this;
            }
        }
        if (e.OldItems is not null)
        {
            foreach (TreeNode child in e.OldItems)
            {
                if (child.Parent == this) child.Parent = null;
            }
        }
    }
}
