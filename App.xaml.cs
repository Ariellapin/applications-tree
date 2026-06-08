using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
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

    private MainWindow? _mainWindow;
    private IconResolver? _icons;
    private ConfigLoader? _config;
    private readonly ObservableCollection<TreeNode> _items = new();

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

        _mainWindow = new MainWindow();
        var popup = _mainWindow.Popup;
        popup.Items = _items;
        popup.ItemLaunched += () => _mainWindow?.Hide();
        popup.AddRequested += OnAddRequested;
        popup.ReloadRequested += () => _config?.Reload();
        popup.OpenConfigRequested += OnOpenConfig;
        popup.AutoStartToggled += OnAutoStartToggled;
        popup.ExitRequested += () => Shutdown();
        popup.MenuOpening += () => popup.AutoStartChecked = AutoStartService.IsEnabled();
        popup.ChangeIconRequested += OnChangeIconRequested;
        popup.RenameCommitted += OnRenameCommitted;
        popup.DeleteRequested += OnDeleteRequested;
        _mainWindow.Show();

        _icons = new IconResolver();
        _config = new ConfigLoader(_icons, Dispatcher);
        _config.Loaded += OnConfigLoaded;
        _config.LoadFailed += OnConfigFailed;
        _config.Reload();
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
        Dispatcher.Invoke(() => ShowError("Config error", message));
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnAddRequested()
    {
        var dlg = new AddEntryDialog(_items);
        if (dlg.ShowDialog() != true || dlg.Result is null) return;

        try
        {
            AppendEntry(dlg.Result);
            ConfigSerializer.Save(_items);
        }
        catch (Exception ex)
        {
            ShowError("Save failed", ex.Message);
        }
    }

    private void AppendEntry(AddEntryDialog.EntryResult r)
    {
        TreeNode node;
        if (r.IsFolder)
        {
            node = new FolderNode
            {
                Name = r.Name,
                Icon = _icons!.GetFolderIcon(),
                IsExpanded = true,
            };
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

    private void OnChangeIconRequested(TreeNode node)
    {
        var initialPath = node is ItemNode it ? it.Path : null;
        var dlg = new IconPickerDialog(node.IconSource, initialPath)
        {
            Owner = _mainWindow,
        };
        if (dlg.ShowDialog() != true || dlg.Result is null) return;

        var newSource = dlg.Result.IconSource;
        node.IconSource = newSource;
        node.Icon = ResolveIcon(node, newSource);

        SaveConfigSafe();
    }

    private void OnRenameCommitted(TreeNode node)
    {
        SaveConfigSafe();
    }

    private void OnDeleteRequested(TreeNode node)
    {
        if (node is FolderNode folder && folder.Children.Count > 0)
        {
            var msg = folder.Children.Count == 1
                ? $"Delete folder \"{folder.Name}\" and its 1 item?"
                : $"Delete folder \"{folder.Name}\" and its {folder.Children.Count} items?";
            var result = MessageBox.Show(
                _mainWindow!, msg, "Confirm delete",
                MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (result != MessageBoxResult.OK) return;
        }

        if (node.Parent is FolderNode parent)
        {
            parent.Children.Remove(node);
        }
        else
        {
            _items.Remove(node);
        }

        SaveConfigSafe();
    }

    private System.Windows.Media.ImageSource? ResolveIcon(TreeNode node, string? source)
    {
        var custom = !string.IsNullOrWhiteSpace(source)
            ? _icons!.GetIconFromSource(source!, smallSize: true)
            : null;
        if (custom is not null) return custom;
        return node switch
        {
            ItemNode i => _icons!.GetIcon(i.Path, i.Kind),
            FolderNode => _icons!.GetFolderIcon(),
            _ => null,
        };
    }

    private void SaveConfigSafe()
    {
        try
        {
            ConfigSerializer.Save(_items);
        }
        catch (Exception ex)
        {
            ShowError("Save failed", ex.Message);
        }
    }

    private void OnOpenConfig()
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
            ShowError("Could not open config", ex.Message);
        }
    }

    private void OnAutoStartToggled(bool enabled)
    {
        try
        {
            AutoStartService.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            ShowError("Auto-start failed", ex.Message);
            if (_mainWindow is not null)
                _mainWindow.Popup.AutoStartChecked = AutoStartService.IsEnabled();
        }
    }

    private void SignalListener()
    {
        var token = _signalCts!.Token;
        var handles = new WaitHandle[] { _signal!, token.WaitHandle };
        while (!token.IsCancellationRequested)
        {
            var idx = WaitHandle.WaitAny(handles);
            if (idx == 1) return;
            Dispatcher.BeginInvoke(() => _mainWindow?.BringToFront());
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _signalCts?.Cancel();
        _signal?.Dispose();
        _config?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
