using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
    public event Action? ReloadRequested;
    public event Action? OpenConfigRequested;
    public event Action<bool>? AutoStartToggled;
    public event Action? ExitRequested;
    public event Action? MenuOpening;
    public event Action<TreeNode>? ChangeIconRequested;
    public event Action<TreeNode>? RenameCommitted;
    public event Action<TreeNode>? DeleteRequested;

    public bool AutoStartChecked
    {
        get => AutoStartMenuItem.IsChecked;
        set => AutoStartMenuItem.IsChecked = value;
    }

    public TreePopup()
    {
        InitializeComponent();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddRequested?.Invoke();
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is { } cm)
        {
            MenuOpening?.Invoke();
            cm.PlacementTarget = btn;
            cm.Placement = PlacementMode.Bottom;
            cm.IsOpen = true;
        }
    }

    private void OnReloadClick(object sender, RoutedEventArgs e) => ReloadRequested?.Invoke();
    private void OnOpenConfigClick(object sender, RoutedEventArgs e) => OpenConfigRequested?.Invoke();
    private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi) AutoStartToggled?.Invoke(mi.IsChecked);
    }

    private static TreeNode? NodeFromMenuItem(MenuItem mi)
    {
        if (mi.DataContext is TreeNode direct) return direct;
        if (mi.Parent is ContextMenu cm &&
            cm.PlacementTarget is FrameworkElement fe &&
            fe.DataContext is TreeNode viaTarget)
        {
            return viaTarget;
        }
        return null;
    }

    private void OnChangeIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var node = NodeFromMenuItem(mi);
        if (node is not null) ChangeIconRequested?.Invoke(node);
    }

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var node = NodeFromMenuItem(mi);
        if (node is not null) node.IsEditing = true;
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var node = NodeFromMenuItem(mi);
        if (node is not null) DeleteRequested?.Invoke(node);
    }

    private void OnRenameBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            // Defer so the template's container is fully realized before we focus it.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                Keyboard.Focus(tb);
                tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnRenameBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not TreeNode node) return;

        if (e.Key == Key.Enter)
        {
            CommitRename(tb, node);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename(tb, node);
            e.Handled = true;
        }
    }

    private void OnRenameBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not TreeNode node) return;
        if (!node.IsEditing) return;
        CommitRename(tb, node);
    }

    private void CommitRename(TextBox tb, TreeNode node)
    {
        var proposed = tb.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(proposed))
        {
            CancelRename(tb, node);
            return;
        }
        var be = tb.GetBindingExpression(TextBox.TextProperty);
        be?.UpdateSource();
        node.IsEditing = false;
        RenameCommitted?.Invoke(node);
    }

    private void CancelRename(TextBox tb, TreeNode node)
    {
        var be = tb.GetBindingExpression(TextBox.TextProperty);
        be?.UpdateTarget();
        node.IsEditing = false;
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ItemNode item)
        {
            if (item.IsEditing) return;
            var (ok, error) = Launcher.Launch(item);
            if (!ok)
            {
                ((App)Application.Current).ShowError(
                    "Failed to launch", $"{item.Name}: {error}");
            }
            ItemLaunched?.Invoke();
            e.Handled = true;
        }
    }
}
