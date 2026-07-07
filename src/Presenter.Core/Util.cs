using System.ComponentModel;
using System.Text;

namespace Presenter.Core;

/// <summary>
/// Framework-agnostic helpers ported from App_Code/Util.cs. WPF- and COM-specific
/// helpers from the original live in Presenter.App / Presenter.Engine.Com instead.
/// </summary>
public static class Util
{
    public static T? Parse<T>(this object? value)
    {
        try { return (T?)TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(value!.ToString()!); }
        catch (Exception) { return default; }
    }

    public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
    {
        foreach (var elm in source)
            action.Invoke(elm);
    }

    /// <summary>
    /// Makes the first letter of each word upper case and the rest of the letters lower case.
    /// </summary>
    public static string ToFirstUpper(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        value = value.Trim();
        StringBuilder res = new(char.ToUpper(value[0]).ToString());

        for (int i = 1; i < value.Length; i++)
        {
            if (value[i] == ' ')
                res.Append(' ').Append(char.ToUpper(value[++i]));
            else
                res.Append(char.ToLower(value[i]));
        }

        return res.ToString();
    }

    public static int FindIndex<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        int idx = 0;
        foreach (var item in source)
        {
            if (predicate(item))
                return idx;
            idx++;
        }
        return -1;
    }

    public static string FormatTimeSpan(this TimeSpan span, bool showSign)
    {
        string sign = string.Empty;
        if (showSign && span > TimeSpan.Zero)
            sign = "+";

        if ((int)span.TotalHours > 0)
            return sign + ((int)span.TotalHours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");

        if (span.Minutes > 0)
            return sign + span.Minutes + ":" + span.Seconds.ToString("00");

        return sign + span.Seconds;
    }

    public static string? ToNullIfEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Both separators that can appear in stored filenames: a database migrated from
    /// the Windows app contains backslash paths, which macOS/Linux Path APIs don't parse.
    /// </summary>
    public static readonly char[] DirectorySeparators = ['\\', '/'];

    /// <summary>Path.GetFileName equivalent that treats both '\' and '/' as separators.</summary>
    public static string GetFileName(string path)
    {
        int i = path.LastIndexOfAny(DirectorySeparators);
        return i < 0 ? path : path[(i + 1)..];
    }

    /// <summary>Rewrites both '\' and '/' to the platform's directory separator.</summary>
    public static string NormalizeSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}
