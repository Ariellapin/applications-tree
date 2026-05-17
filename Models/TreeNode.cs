using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WPFToolbarTree.Models;

public abstract class TreeNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private ImageSource? _icon;
    private bool _isExpanded;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
