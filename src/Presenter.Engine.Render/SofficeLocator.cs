using System.IO;
using Microsoft.Win32;

namespace Presenter.Engine.Render;

/// <summary>Locates the LibreOffice soffice.exe executable.</summary>
public static class SofficeLocator
{
    public static string? Find()
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

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), "soffice.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception) { }
        }

        return null;
    }
}
