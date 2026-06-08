using System.Windows;
using System.Windows.Input;
using WPFToolbarTree.Views;

namespace WPFToolbarTree;

public partial class MainWindow : Window
{
    public TreePopup Popup => PopupRoot;

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => PositionNearTaskbar();
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Window is always visible; no toggle behavior.
    }

    private void PositionNearTaskbar()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 8;
        Top = wa.Bottom - Height - 8;
    }
}
