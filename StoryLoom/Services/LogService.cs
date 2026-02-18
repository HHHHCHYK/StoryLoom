using System;
using System.IO;

namespace StoryLoom.Services
{
    /// <summary>
    /// 日志服务类。
    /// 提供基本的文件日志记录功能，支持多线程安全写入，日志按会话或日期分文件存储。
    /// </summary>
    public class LogService
    {
        private readonly string? _logFilePath;
        private readonly object _lock = new object();

        /// <summary>
        /// 初始化日志服务。
        /// 检查并创建 Log 目录，生成当前会话的日志文件路径。
        /// </summary>
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

        /// <summary>
        /// 记录一条日志信息。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="level">日志级别（默认 Info）。</param>
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

        /// <summary>
        /// 记录异常详情。
        /// 包含异常类型、消息、堆栈跟踪以及内部异常信息。
        /// </summary>
        /// <param name="ex">异常对象。</param>
        /// <param name="context">异常发生的上下文描述。</param>
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
