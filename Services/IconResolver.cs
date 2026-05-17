using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPFToolbarTree.Models;

namespace WPFToolbarTree.Services;

public sealed class IconResolver
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private ImageSource? _folderIcon;
    private ImageSource? _urlIcon;

    public ImageSource GetIcon(string path, EntryKind kind)
    {
        var key = $"{kind}:{path}";
        if (_cache.TryGetValue(key, out var hit)) return hit;

        ImageSource src = kind switch
        {
            EntryKind.Url => GetUrlIcon(),
            EntryKind.Folder => GetFolderIcon(),
            _ => ExtractFileIcon(path) ?? GetGenericFileIcon(),
        };

        _cache[key] = src;
        return src;
    }

    public ImageSource GetFolderIcon()
    {
        if (_folderIcon is not null) return _folderIcon;
        _folderIcon = ShellIconForAttributes(FILE_ATTRIBUTE_DIRECTORY)
                      ?? CreateFallbackIcon(Colors.Goldenrod);
        return _folderIcon;
    }

    private ImageSource GetUrlIcon()
    {
        if (_urlIcon is not null) return _urlIcon;
        _urlIcon = ShellIconForAttributes(FILE_ATTRIBUTE_NORMAL, ".html")
                   ?? CreateFallbackIcon(Colors.SteelBlue);
        return _urlIcon;
    }

    private ImageSource GetGenericFileIcon() =>
        ShellIconForAttributes(FILE_ATTRIBUTE_NORMAL) ?? CreateFallbackIcon(Colors.Gray);

    private static ImageSource? ExtractFileIcon(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return null;
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            return BitmapFromHIcon(icon.Handle);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? ShellIconForAttributes(uint attributes, string fakeName = "x")
    {
        var info = new SHFILEINFO();
        var flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;
        var hImg = SHGetFileInfo(fakeName, attributes, ref info, (uint)Marshal.SizeOf(info), flags);
        if (hImg == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
        try
        {
            return BitmapFromHIcon(info.hIcon);
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static ImageSource BitmapFromHIcon(IntPtr hIcon)
    {
        var bmp = Imaging.CreateBitmapSourceFromHIcon(
            hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        bmp.Freeze();
        return bmp;
    }

    private static ImageSource CreateFallbackIcon(System.Windows.Media.Color color)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 16, 16));
        }
        var rtb = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    // ---- P/Invoke ----

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
