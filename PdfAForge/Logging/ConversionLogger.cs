using System;
using System.IO;
using PdfAForge.Config;

namespace PdfAForge.Logging
{
    public enum LogLevel { INFO, WARN, ERROR }

    /// <summary>
    /// Rotating daily file logger with automatic retention cleanup.
    /// Thread-safe via lock.
    /// </summary>
    public class ConversionLogger
    {
        private static readonly ConversionLogger _instance = new ConversionLogger();
        public static ConversionLogger Current => _instance;

        private readonly object _lock = new object();

        private ConversionLogger() { }

        // --- Public API ---

        public void Info(string correlationId, string message)
            => Write(LogLevel.INFO, correlationId, message);

        public void Warn(string correlationId, string message)
            => Write(LogLevel.WARN, correlationId, message);

        public void Error(string correlationId, string message, Exception ex = null)
        {
            Write(LogLevel.ERROR, correlationId, message);
            if (ex != null)
            {
                Write(LogLevel.ERROR, correlationId, $"Exception: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Write(LogLevel.ERROR, correlationId, $"Inner: {ex.InnerException.Message}");
                Write(LogLevel.ERROR, correlationId, $"Stack: {ex.StackTrace}");
            }
        }

        public void StartupInfo(string message)
            => Write(LogLevel.INFO, "STARTUP", message);

        public void ConversionSuccess(string correlationId, string fileName,
            long inputKb, long outputKb, long durationMs)
        {
            Write(LogLevel.INFO, correlationId,
                $"SUCCESS | file={fileName} | in={inputKb}kb | out={outputKb}kb | duration={durationMs}ms");
        }

        public void ConversionError(string correlationId, string fileName,
            string reason, Exception ex = null)
        {
            Write(LogLevel.ERROR, correlationId,
                $"FAILED | file={fileName} | reason={reason}");
            if (ex != null)
                Error(correlationId, ex.Message, ex);
        }

        // --- Retention cleanup ---

        public void CleanOldLogs()
        {
            try
            {
                var logPath = AppSettings.Current.LogPath;
                var retention = AppSettings.Current.LogRetentionDays;
                var cutoff = DateTime.Now.AddDays(-retention);
                var files = Directory.GetFiles(logPath, "conversion_*.log");

                foreach (var f in files)
                {
                    if (File.GetLastWriteTime(f) < cutoff)
                    {
                        File.Delete(f);
                        Write(LogLevel.INFO, "CLEANUP", $"Deleted old log: {Path.GetFileName(f)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Write(LogLevel.WARN, "CLEANUP", $"Log cleanup failed: {ex.Message}");
            }
        }

        // --- Core ---

        private void Write(LogLevel level, string correlationId, string message)
        {
            try
            {
                var logPath = AppSettings.Current.LogPath;
                var logFile = Path.Combine(logPath,
                    $"conversion_{DateTime.Now:yyyyMMdd}.log");

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {level,-5} | {correlationId,-36} | {message}";

                lock (_lock)
                {
                    File.AppendAllText(logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never throw from logger
            }
        }
    }
}