using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WPFToolbarTree.Services;

namespace WPFToolbarTree.Views;

public partial class IconPickerDialog : Window
{
    public sealed class IconEntry
    {
        public int Index { get; init; }
        public ImageSource Image { get; init; } = null!;
    }

    public sealed record IconPickerResult(string? IconSource);

    public IconPickerResult? Result { get; private set; }

    private readonly ObservableCollection<IconEntry> _entries = new();
    private string _currentFile = string.Empty;

    public IconPickerDialog(string? initialSource, string? itemPath)
    {
        InitializeComponent();
        IconList.ItemsSource = _entries;

        var (file, index) = ParseInitial(initialSource, itemPath);
        FileBox.Text = file;
        LoadIconsFromFile(file, preselectIndex: index);
    }

    private static (string File, int Index) ParseInitial(string? initialSource, string? itemPath)
    {
        if (!string.IsNullOrWhiteSpace(initialSource))
        {
            var (f, i) = IconResolver.ParseIconRef(initialSource!);
            if (!string.IsNullOrEmpty(f)) return (f!, i);
        }

        // Default to imageres.dll — it has a rich set of icons useful for .bat/.lnk.
        var win = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var imageres = Path.Combine(win, "imageres.dll");
        if (File.Exists(imageres)) return (imageres, 0);

        return (Path.Combine(win, "shell32.dll"), 0);
    }

    private void LoadIconsFromFile(string file, int preselectIndex = 0)
    {
        _entries.Clear();
        _currentFile = file;
        var expanded = Environment.ExpandEnvironmentVariables(file);
        if (!File.Exists(expanded))
        {
            StatusLabel.Text = $"File not found: {expanded}";
            return;
        }

        var icons = IconResolver.EnumerateIcons(expanded, smallSize: false);
        if (icons.Count == 0)
        {
            // Some files only have small icons.
            icons = IconResolver.EnumerateIcons(expanded, smallSize: true);
        }

        foreach (var (idx, img) in icons)
            _entries.Add(new IconEntry { Index = idx, Image = img });

        StatusLabel.Text = $"{_entries.Count} icon(s) in {Path.GetFileName(expanded)}";

        if (_entries.Count > 0)
        {
            var sel = _entries.FirstOrDefault(e => e.Index == preselectIndex) ?? _entries[0];
            IconList.SelectedItem = sel;
            IconList.ScrollIntoView(sel);
        }
    }

    private void OnFileBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoadIconsFromFile(FileBox.Text.Trim());
            e.Handled = true;
        }
    }

    private void OnLoadClick(object sender, RoutedEventArgs e)
    {
        LoadIconsFromFile(FileBox.Text.Trim());
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose an icon source",
            Filter = "Icon sources (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (string.IsNullOrEmpty(dlg.InitialDirectory) &&
            File.Exists(_currentFile))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(_currentFile);
        }
        if (dlg.ShowDialog(this) == true)
        {
            FileBox.Text = dlg.FileName;
            LoadIconsFromFile(dlg.FileName);
        }
    }

    private void OnIconDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IconList.SelectedItem is IconEntry) OnOk(sender, e);
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        Result = new IconPickerResult(null);
        DialogResult = true;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (IconList.SelectedItem is not IconEntry entry)
        {
            MessageBox.Show(this, "Select an icon first.", "No icon selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Result = new IconPickerResult(IconResolver.FormatIconRef(_currentFile, entry.Index));
        DialogResult = true;
    }
}
