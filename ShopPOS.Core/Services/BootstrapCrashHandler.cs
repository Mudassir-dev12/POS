using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ShopPOS.Core.Services;

public record BootstrapErrorInfo(
    string PlainLanguageAdvice,
    string TechnicalDetails,
    string LogFilePath,
    string ErrorSummary
);

/// <summary>
/// Catches and maps early boot exceptions before Serilog/DI start, providing plain-language advice
/// and logging diagnostics to %LocalAppData%\ShopPOS\startup-error.log.
/// </summary>
public static class BootstrapCrashHandler
{
    public static string DefaultLogFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopPOS", "startup-error.log");

    public static BootstrapErrorInfo MapException(Exception ex, string? logFilePath = null)
    {
        string logPath = logFilePath ?? DefaultLogFilePath;
        Exception root = ex;
        while ((root is TypeInitializationException or AggregateException) && root.InnerException != null)
        {
            root = root.InnerException;
        }

        string advice;
        string summary;

        switch (root)
        {
            case DllNotFoundException dllEx:
                string msg = dllEx.Message.ToLowerInvariant();
                if (msg.Contains("sqlite") || msg.Contains("e_sqlite3"))
                {
                    summary = "SQLite native database library missing";
                    advice = "The SQLite native engine (e_sqlite3.dll) failed to load. Visual C++ runtime missing: reinstall using the setup file.";
                }
                else if (msg.Contains("skia") || msg.Contains("quest") || msg.Contains("harfbuzz"))
                {
                    summary = "PDF & graphics native library missing";
                    advice = "The PDF and graphics rendering engine (QuestPdfSkia.dll) failed to load. Visual C++ runtime missing: reinstall using the setup file.";
                }
                else if (msg.Contains("vcruntime") || msg.Contains("msvcp"))
                {
                    summary = "Visual C++ Runtime missing";
                    advice = "Visual C++ runtime missing: reinstall using the setup file or run prerequisites\\vc_redist.x64.exe.";
                }
                else
                {
                    summary = "Native library failed to load";
                    advice = $"A required system or native component ({dllEx.Message}) is missing. Visual C++ runtime missing: reinstall using the setup file.";
                }
                break;

            case BadImageFormatException:
                summary = "Application architecture mismatch";
                advice = "64-bit architecture mismatch: ShopPOS requires a 64-bit Windows OS and 64-bit native components. Please reinstall using the 64-bit installer.";
                break;

            case FileNotFoundException fnf:
                summary = "Application file or assembly missing";
                advice = $"A required program assembly or configuration file is missing: {fnf.FileName ?? fnf.Message}. Please reinstall ShopPOS.";
                break;

            case UnauthorizedAccessException uae:
                summary = "Permission denied";
                advice = $"Permission denied accessing files or folders: {uae.Message}. Ensure your Windows user account has write permission to the ShopPOS data directory, or run setup again.";
                break;

            case IOException ioEx when ioEx.Message.Contains("space", StringComparison.OrdinalIgnoreCase):
                summary = "Disk space error";
                advice = "Insufficient disk space to initialize the database or write logs. Please free up disk space and try again.";
                break;

            case IOException ioEx:
                summary = "Filesystem or disk locked error";
                advice = $"Filesystem or disk I/O error: {ioEx.Message}. Check that another process is not locking the database files.";
                break;

            default:
                if (root.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    summary = "Database engine error";
                    advice = $"The database engine encountered an error: {root.Message}. If the database is locked or corrupted, run the restore or setup tool.";
                }
                else
                {
                    summary = "Application startup error";
                    advice = $"ShopPOS encountered an unexpected startup failure: {root.Message}.";
                }
                break;
        }

        string technicalDetails = $"[Timestamp]: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                  $"[OS]: {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})\n" +
                                  $"[Framework]: {RuntimeInformation.FrameworkDescription}\n" +
                                  $"[Exception]: {root.GetType().FullName}: {root.Message}\n" +
                                  $"[CallStack]:\n{ex}";

        return new BootstrapErrorInfo(advice, technicalDetails, logPath, summary);
    }

    public static BootstrapErrorInfo LogAndMap(Exception ex, string? logFilePath = null)
    {
        var info = MapException(ex, logFilePath);
        try
        {
            string? dir = Path.GetDirectoryName(info.LogFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string logEntry = $"================================================================================\n" +
                              $"ShopPOS Startup Failure Report\n" +
                              $"================================================================================\n" +
                              $"Summary:  {info.ErrorSummary}\n" +
                              $"Advice:   {info.PlainLanguageAdvice}\n\n" +
                              $"Technical Details:\n{info.TechnicalDetails}\n\n";

            File.AppendAllText(info.LogFilePath, logEntry);
        }
        catch
        {
            // Logging to startup-error.log must never throw
        }

        return info;
    }
}

