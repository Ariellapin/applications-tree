using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using WPFToolbarTree.Models;

namespace WPFToolbarTree.Views;

public partial class AddEntryDialog : Window
{
    public sealed record ParentOption(string Label, FolderNode? Folder);

    public sealed record EntryResult(
        bool IsFolder, string Name, string? Path, FolderNode? Parent);

    public EntryResult? Result { get; private set; }

    public AddEntryDialog(IEnumerable<TreeNode> currentItems)
    {
        InitializeComponent();
        var options = new ObservableCollection<ParentOption>
        {
            new("(Top level)", null),
        };
        foreach (var (label, folder) in FlattenFolders(currentItems, ""))
            options.Add(new ParentOption(label, folder));
        ParentBox.ItemsSource = options;
        ParentBox.SelectedIndex = 0;
        NameBox.Focus();
    }

    private static IEnumerable<(string Label, FolderNode Folder)> FlattenFolders(
        IEnumerable<TreeNode> nodes, string prefix)
    {
        foreach (var n in nodes)
        {
            if (n is FolderNode f)
            {
                var label = string.IsNullOrEmpty(prefix) ? f.Name : $"{prefix} / {f.Name}";
                yield return (label, f);
                foreach (var inner in FlattenFolders(f.Children, label))
                    yield return inner;
            }
        }
    }

    private void OnTypeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var isFolder = FolderRadio.IsChecked == true;
        PathLabel.Visibility = isFolder ? Visibility.Collapsed : Visibility.Visible;
        PathBox.Visibility = isFolder ? Visibility.Collapsed : Visibility.Visible;
        BrowseBtn.Visibility = isFolder ? Visibility.Collapsed : Visibility.Visible;
        HintLabel.Text = isFolder
            ? "A folder is a grouping node. You can drop items inside it later."
            : "Files, .exe, .lnk, folders, and http(s) URLs are all supported.";
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select a file or shortcut",
            Filter = "All files (*.*)|*.*|Programs (*.exe)|*.exe|Shortcuts (*.lnk)|*.lnk",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) == true)
        {
            PathBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? "";
        var isFolder = FolderRadio.IsChecked == true;
        var path = PathBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Please enter a name.", "Missing name",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }
        if (!isFolder && string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(this, "Please enter a path or URL.", "Missing path",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            PathBox.Focus();
            return;
        }

        var parent = (ParentBox.SelectedItem as ParentOption)?.Folder;
        Result = new EntryResult(isFolder, name, isFolder ? null : path, parent);
        DialogResult = true;
    }
}
