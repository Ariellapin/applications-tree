using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using WPFToolbarTree.Config;
using WPFToolbarTree.Models;
using WPFToolbarTree.Services;
using WPFToolbarTree.Views;

namespace WPFToolbarTree;

public partial class App : Application
{
    private const string MutexName = "WPFToolbarTree.Singleton.Mutex";
    private const string SignalName = "WPFToolbarTree.Singleton.Signal";

    private Mutex? _mutex;
    private EventWaitHandle? _signal;
    private Thread? _signalThread;
    private CancellationTokenSource? _signalCts;

    private TaskbarIcon? _tray;
    private TreePopup? _popup;
    private MenuItem? _autoStartMenuItem;

    private IconResolver? _icons;
    private ConfigLoader? _config;
    private readonly ObservableCollection<TreeNode> _items = new();

    private bool _pinned;
    private DispatcherTimer? _hoverCloseTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        if (!created)
        {
            try
            {
                var signal = EventWaitHandle.OpenExisting(SignalName);
                signal.Set();
            }
            catch { /* existing instance gone */ }
            Shutdown();
            return;
        }

        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        _signalCts = new CancellationTokenSource();
        _signalThread = new Thread(SignalListener) { IsBackground = true };
        _signalThread.Start();

        _tray = (TaskbarIcon)FindResource("TrayIcon");
        _tray.TrayMouseMove += OnTrayMouseMove;
        _tray.TrayLeftMouseDown += OnTrayLeftClick;
        _tray.PreviewTrayContextMenuOpen += (_, _) => SyncAutoStartCheck();
        _tray.TrayPopupOpen += (_, _) =>
        {
            CancelHoverClose();
            HookPopupClosed();
        };

        _popup = (TreePopup)_tray.TrayPopup;
        _popup.Items = _items;
        _popup.ItemLaunched += () => ClosePopup(force: true);
        _popup.AddRequested += OnAddRequested;
        _popup.MouseEnter += (_, _) => CancelHoverClose();
        _popup.MouseLeave += (_, _) => StartHoverClose();

        _autoStartMenuItem = _tray.ContextMenu.Items
            .OfType<MenuItem>()
            .First(m => (string)m.Header == "Start with Windows");

        _hoverCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _hoverCloseTimer.Tick += (_, _) =>
        {
            _hoverCloseTimer!.Stop();
            if (!_pinned) ClosePopup();
        };

        _icons = new IconResolver();
        _config = new ConfigLoader(_icons, Dispatcher);
        _config.Loaded += OnConfigLoaded;
        _config.LoadFailed += OnConfigFailed;
        _config.Reload();
    }

    private int _moveCount;
    private void OnTrayMouseMove(object? sender, RoutedEventArgs e)
    {
        Interlocked.Increment(ref _moveCount);
        if (_moveCount == 1) DiagLog("first TrayMouseMove fired");
        if (_pinned) { DiagLog("ignored: pinned"); return; }
        if (IsTrayPopupOpen())
        {
            CancelHoverClose();
            return;
        }
        // Open immediately on hover — TaskbarIcon has no MouseEnter event so we
        // open on the first move; subsequent moves are no-ops because the popup
        // is already open.
        OpenPopup();
    }

    private static void DiagLog(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                AppPaths.ErrorLog,
                $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private void OnTrayLeftClick(object? sender, RoutedEventArgs e)
    {
        DiagLog("TrayLeftMouseDown fired");
        if (_tray is null) return;
        if (IsTrayPopupOpen())
        {
            _pinned = false;
            ClosePopup(force: true);
        }
        else
        {
            _pinned = true;
            OpenPopup();
        }
    }

    private void OpenPopup()
    {
        if (_tray is null) return;
        CancelHoverClose();
        if (!IsTrayPopupOpen()) _tray.ShowTrayPopup();
    }

    private void ClosePopup(bool force = false)
    {
        if (_tray is null) return;
        if (_pinned && !force) return;
        if (IsTrayPopupOpen()) _tray.CloseTrayPopup();
        _pinned = false;
    }

    private void StartHoverClose()
    {
        if (_pinned) return;
        _hoverCloseTimer?.Stop();
        _hoverCloseTimer?.Start();
    }

    private void CancelHoverClose() => _hoverCloseTimer?.Stop();

    private bool IsTrayPopupOpen() => _tray?.TrayPopupResolved?.IsOpen ?? false;

    private bool _popupClosedHooked;
    private void HookPopupClosed()
    {
        if (_popupClosedHooked || _tray?.TrayPopupResolved is not { } popup) return;
        popup.Closed += (_, _) =>
        {
            // Reset state whenever the popup closes for any reason
            // (focus loss, click-outside, our own CloseTrayPopup, etc.).
            _pinned = false;
            _hoverCloseTimer?.Stop();
        };
        _popupClosedHooked = true;
    }

    private void OnConfigLoaded(List<TreeNode> nodes)
    {
        Dispatcher.Invoke(() =>
        {
            _items.Clear();
            foreach (var n in nodes) _items.Add(n);
        });
    }

    private void OnConfigFailed(string message)
    {
        Dispatcher.Invoke(() => ShowBalloon("Config error", message, BalloonIcon.Error));
    }

    public void ShowBalloon(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        _tray?.ShowBalloonTip(title, message, icon);
    }

    // ---- Add entry flow ----

    private void OnAddRequested()
    {
        ClosePopup(force: true);
        var dlg = new Views.AddEntryDialog(_items);
        if (dlg.ShowDialog() != true || dlg.Result is null) return;

        try
        {
            AppendEntry(dlg.Result);
            ConfigSerializer.Save(_items);
        }
        catch (Exception ex)
        {
            ShowBalloon("Save failed", ex.Message, BalloonIcon.Error);
        }
    }

    private void AppendEntry(Views.AddEntryDialog.EntryResult r)
    {
        TreeNode node;
        if (r.IsFolder)
        {
            node = new FolderNode { Name = r.Name, Icon = _icons!.GetFolderIcon() };
        }
        else
        {
            var raw = r.Path ?? string.Empty;
            var expanded = Environment.ExpandEnvironmentVariables(raw);
            var kind = ConfigLoader.ClassifyPath(expanded);
            node = new ItemNode
            {
                Name = r.Name,
                Path = expanded,
                RawPath = raw,
                Kind = kind,
                Icon = _icons!.GetIcon(expanded, kind),
            };
        }
        if (r.Parent is null) _items.Add(node);
        else r.Parent.Children.Add(node);
    }

    // ---- Context menu handlers ----

    private void OnReloadClick(object sender, RoutedEventArgs e) => _config?.Reload();

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.ConfigFile,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowBalloon("Could not open config", ex.Message, BalloonIcon.Error);
        }
    }

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            try
            {
                AutoStartService.SetEnabled(mi.IsChecked);
            }
            catch (Exception ex)
            {
                ShowBalloon("Auto-start failed", ex.Message, BalloonIcon.Error);
                mi.IsChecked = AutoStartService.IsEnabled();
            }
        }
    }

    private void SyncAutoStartCheck()
    {
        if (_autoStartMenuItem is null) return;
        _autoStartMenuItem.IsChecked = AutoStartService.IsEnabled();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Shutdown();

    // ---- Single-instance signal ----

    private void SignalListener()
    {
        var token = _signalCts!.Token;
        var handles = new WaitHandle[] { _signal!, token.WaitHandle };
        while (!token.IsCancellationRequested)
        {
            var idx = WaitHandle.WaitAny(handles);
            if (idx == 1) return;
            Dispatcher.BeginInvoke(() =>
            {
                _pinned = true;
                OpenPopup();
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _signalCts?.Cancel();
        _signal?.Dispose();
        _config?.Dispose();
        _tray?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
