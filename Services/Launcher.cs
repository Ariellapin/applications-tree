using System.Diagnostics;
using WPFToolbarTree.Models;

namespace WPFToolbarTree.Services;

public static class Launcher
{
    public static (bool Ok, string? Error) Launch(ItemNode item)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = item.Path,
                UseShellExecute = true,
            };
            Process.Start(psi);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
