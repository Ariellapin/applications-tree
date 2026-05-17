using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFToolbarTree.Models;
using WPFToolbarTree.Services;

namespace WPFToolbarTree.Views;

public partial class TreePopup : UserControl
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(ObservableCollection<TreeNode>),
        typeof(TreePopup),
        new PropertyMetadata(null));

    public ObservableCollection<TreeNode>? Items
    {
        get => (ObservableCollection<TreeNode>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public event Action? ItemLaunched;
    public event Action? AddRequested;

    public TreePopup()
    {
        InitializeComponent();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddRequested?.Invoke();
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ItemNode item)
        {
            var (ok, error) = Launcher.Launch(item);
            if (!ok)
            {
                ((App)Application.Current).ShowBalloon(
                    "Failed to launch", $"{item.Name}: {error}");
            }
            ItemLaunched?.Invoke();
            e.Handled = true;
        }
    }
}
