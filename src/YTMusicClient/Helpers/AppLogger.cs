using System;
using System.IO;

namespace YTMusicClient.Helpers;

public static class AppLogger
{
    private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "YTMusicClient.log");
    private static readonly object LockObj = new();

    public static void Log(string message)
    {
        try
        {
            lock (LockObj)
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, entry);
                System.Diagnostics.Debug.WriteLine(entry);
            }
        }
        catch { }
    }

    public static void LogError(string context, Exception ex)
    {
        Log($"[ERROR] {context}: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
    }
}
