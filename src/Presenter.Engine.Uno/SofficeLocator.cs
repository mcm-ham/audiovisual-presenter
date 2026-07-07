using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Presenter.Engine.Uno;

/// <summary>Locates the LibreOffice soffice executable per platform.</summary>
public static class SofficeLocator
{
    public static string? Find()
    {
        if (OperatingSystem.IsWindows())
            return FindWindows();
        if (OperatingSystem.IsMacOS())
            return FindMacOS();
        return FindOnPath("soffice");
    }

    /// <summary>
    /// The bundled Python interpreter used to run slideshow_host.py out of process.
    /// Windows-only: on macOS the bundled Python is signed with parent launch
    /// constraints (only soffice may spawn it), so the script runs in-process via
    /// the script provider instead and no interpreter path is needed.
    /// </summary>
    public static string? FindPython(string soffice)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        string python = Path.Combine(Path.GetDirectoryName(soffice)!, "python.exe");
        return File.Exists(python) ? python : null;
    }

    [SupportedOSPlatform("windows")]
    private static string? FindWindows()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe");
            if (key?.GetValue(null) is string path && File.Exists(path))
                return path;
        }

        foreach (var root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        })
        {
            if (string.IsNullOrEmpty(root))
                continue;
            string candidate = Path.Combine(root, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return FindOnPath("soffice.exe");
    }

    private static string? FindMacOS()
    {
        foreach (var root in new[]
        {
            "/Applications",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications"),
        })
        {
            string candidate = Path.Combine(root, "LibreOffice.app", "Contents", "MacOS", "soffice");
            if (File.Exists(candidate))
                return candidate;
        }

        return FindOnPath("soffice");
    }

    private static string? FindOnPath(string exeName)
    {
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception) { }
        }
        return null;
    }
}
