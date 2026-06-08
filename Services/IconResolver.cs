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

    /// <summary>
    /// Loads an icon from "path,index" (Windows convention). Index optional; defaults to 0.
    /// Environment variables in the path are expanded. Returns null on failure.
    /// </summary>
    public ImageSource? GetIconFromSource(string source, bool smallSize)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        var key = $"iconsrc:{smallSize}:{source}";
        if (_cache.TryGetValue(key, out var hit)) return hit;

        var (file, index) = ParseIconRef(source);
        if (file is null) return null;

        var icon = ExtractIconAt(file, index, smallSize);
        if (icon is not null) _cache[key] = icon;
        return icon;
    }

    /// <summary>
    /// Enumerates all icons in a PE file (.exe/.dll/.ico) at the given size.
    /// Returns list of (index, image) pairs.
    /// </summary>
    public static List<(int Index, ImageSource Image)> EnumerateIcons(string file, bool smallSize)
    {
        var result = new List<(int, ImageSource)>();
        var expanded = Environment.ExpandEnvironmentVariables(file);
        if (!File.Exists(expanded)) return result;

        int count = ExtractIconEx(expanded, -1, null, null, 0);
        if (count <= 0) return result;

        // Extract in modest batches to keep handle pressure reasonable.
        var large = new IntPtr[count];
        var small = new IntPtr[count];
        var got = ExtractIconEx(expanded, 0, large, small, (uint)count);
        if (got <= 0) return result;

        for (int i = 0; i < got; i++)
        {
            var h = smallSize ? small[i] : large[i];
            if (h == IntPtr.Zero) continue;
            try
            {
                result.Add((i, BitmapFromHIcon(h)));
            }
            catch { /* skip bad slot */ }
        }

        for (int i = 0; i < got; i++)
        {
            if (large[i] != IntPtr.Zero) DestroyIcon(large[i]);
            if (small[i] != IntPtr.Zero) DestroyIcon(small[i]);
        }
        return result;
    }

    private static ImageSource? ExtractIconAt(string file, int index, bool smallSize)
    {
        var expanded = Environment.ExpandEnvironmentVariables(file);
        if (!File.Exists(expanded)) return null;

        var large = new IntPtr[1];
        var small = new IntPtr[1];
        var got = ExtractIconEx(expanded, index, large, small, 1);
        if (got <= 0) return null;
        try
        {
            var h = smallSize ? small[0] : large[0];
            if (h == IntPtr.Zero) h = smallSize ? large[0] : small[0];
            if (h == IntPtr.Zero) return null;
            return BitmapFromHIcon(h);
        }
        finally
        {
            if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
            if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
        }
    }

    public static (string? File, int Index) ParseIconRef(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return (null, 0);
        // Format: "path,index" — but paths can contain commas, so split on the LAST comma
        // only if the trailing piece is a valid integer.
        var idx = source.LastIndexOf(',');
        if (idx > 0 && idx < source.Length - 1
            && int.TryParse(source.AsSpan(idx + 1), out var i))
        {
            return (source[..idx].Trim(), i);
        }
        return (source.Trim(), 0);
    }

    public static string FormatIconRef(string file, int index) =>
        index == 0 ? file : $"{file},{index}";

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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex,
        IntPtr[]? phIconLarge, IntPtr[]? phIconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
