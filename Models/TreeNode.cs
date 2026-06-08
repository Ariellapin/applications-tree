using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WPFToolbarTree.Models;

public abstract class TreeNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private ImageSource? _icon;
    private bool _isExpanded;
    private bool _isEditing;
    private FolderNode? _parent;

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
                NotifyFullPathChanged();
        }
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    // Custom icon override. Format: "file,index" (Windows convention).
    public string? IconSource { get; set; }

    public FolderNode? Parent
    {
        get => _parent;
        internal set
        {
            if (Set(ref _parent, value))
                NotifyFullPathChanged();
        }
    }

    public string FullPath =>
        _parent is null ? Name : _parent.FullPath + " / " + Name;

    public virtual string ToolTipText => FullPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        return true;
    }

    protected internal void NotifyFullPathChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullPath)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTipText)));
        if (this is FolderNode folder)
        {
            foreach (var child in folder.Children)
                child.NotifyFullPathChanged();
        }
    }
}
