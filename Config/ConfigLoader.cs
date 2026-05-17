using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using WPFToolbarTree.Models;
using WPFToolbarTree.Services;

namespace WPFToolbarTree.Config;

public sealed class ConfigLoader : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IconResolver _icons;
    private readonly Dispatcher _dispatcher;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _debounce;

    public event Action<List<TreeNode>>? Loaded;
    public event Action<string>? LoadFailed;

    public ConfigLoader(IconResolver icons, Dispatcher dispatcher)
    {
        _icons = icons;
        _dispatcher = dispatcher;

        AppPaths.EnsureDataDir();
        DefaultConfig.WriteIfMissing();

        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Reload(); };

        _watcher = new FileSystemWatcher(AppPaths.DataDir, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Created += (_, _) => ScheduleReload();
        _watcher.Renamed += (_, _) => ScheduleReload();
    }

    private void ScheduleReload()
    {
        _dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
    }

    public void Reload()
    {
        try
        {
            using var stream = new FileStream(
                AppPaths.ConfigFile,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var root = JsonSerializer.Deserialize<RootDto>(stream, JsonOpts)
                       ?? new RootDto();
            var nodes = root.Items.Select(Build).ToList();
            Loaded?.Invoke(nodes);
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(
                    AppPaths.ErrorLog,
                    $"[{DateTime.Now:O}] Config load failed: {ex}{Environment.NewLine}");
            }
            catch { /* don't cascade */ }
            LoadFailed?.Invoke(ex.Message);
        }
    }

    private TreeNode Build(NodeDto dto)
    {
        if (string.Equals(dto.Type, "folder", StringComparison.OrdinalIgnoreCase))
        {
            var folder = new FolderNode { Name = dto.Name, Icon = _icons.GetFolderIcon() };
            if (dto.Children is not null)
                foreach (var child in dto.Children)
                    folder.Children.Add(Build(child));
            return folder;
        }

        var rawPath = dto.Path ?? string.Empty;
        var expanded = Environment.ExpandEnvironmentVariables(rawPath);
        var kind = ClassifyPath(expanded);
        var displayName = string.IsNullOrWhiteSpace(dto.Name)
            ? System.IO.Path.GetFileNameWithoutExtension(expanded)
            : dto.Name;

        return new ItemNode
        {
            Name = displayName,
            Path = expanded,
            RawPath = rawPath,
            Kind = kind,
            Icon = _icons.GetIcon(expanded, kind),
        };
    }

    public static EntryKind ClassifyPath(string expanded)
    {
        if (Uri.TryCreate(expanded, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return EntryKind.Url;

        if (Directory.Exists(expanded)) return EntryKind.Folder;

        var ext = System.IO.Path.GetExtension(expanded).ToLowerInvariant();
        return ext switch
        {
            ".exe" => EntryKind.Executable,
            ".lnk" => EntryKind.Shortcut,
            _      => EntryKind.Document,
        };
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Stop();
    }
}
