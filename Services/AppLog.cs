using System.Diagnostics;

namespace LocalMind.Services;

public static class AppLog
{
    private const int RetentionDays = 14;

    private static readonly object Gate = new();
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalMind", "logs");

    public static string DirectoryPath => LogDir;

    private static string CurrentPath => Path.Combine(LogDir, $"localmind-{DateTimeOffset.Now:yyyyMMdd}.log");

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            DeleteOldLogs();
            Info($"Starting LocalMind {typeof(AppLog).Assembly.GetName().Version} from {Environment.ProcessPath}");
        }
        catch
        {
        }
    }

    public static void Info(string message)
        => Write("INFO", message);

    public static void Warning(string message, Exception exception)
        => Write("WARN", message, exception);

    public static void Error(string message, Exception exception)
        => Write("ERROR", message, exception);

    public static void OpenDirectory()
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Error("Failed to open log directory.", ex);
        }
    }

    private static void Write(string level, string message, Exception exception = null)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var entry = $"{DateTimeOffset.Now:O} [{level}] {message}";
            if (exception is not null)
            {
                entry += Environment.NewLine + exception;
            }

            lock (Gate)
            {
                File.AppendAllText(CurrentPath, entry + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private static void DeleteOldLogs()
    {
        var cutoff = DateTimeOffset.Now.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(LogDir, "localmind-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }
}