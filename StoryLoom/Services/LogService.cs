using System;
using System.IO;

namespace StoryLoom.Services
{
    public class LogService
    {
        private readonly string? _logFilePath;
        private readonly object _lock = new object();

        public LogService()
        {
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // Create a unique file name per session
                var fileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                _logFilePath = Path.Combine(logDir, fileName);

                Log("LogService initialized.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // Fallback: If we can't create log file, write to debug console
                System.Diagnostics.Debug.WriteLine($"Failed to initialize LogService: {ex}");
            }
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (string.IsNullOrEmpty(_logFilePath)) return;

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex}");
                }
            }
        }

        public void LogError(Exception ex, string context = "")
        {
            var message = $"EXCEPTION {context}: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}";
            if (ex.InnerException != null)
            {
                message += $"\nInner Exception: {ex.InnerException.Message}";
            }
            Log(message, LogLevel.Error);
        }
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }
}
