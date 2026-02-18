using System;
using System.Threading.Tasks;
using System.Windows;

namespace StoryLoom
{
    // 这里肯定被 Rider 错改成了 public partial class MainUI : Application
    // 请把它改回 App！
    /// <summary>
    /// App.xaml 的交互逻辑。
    /// 负责应用程序的启动流程以及全局异常捕获和日志记录。
    /// </summary>
    public partial class App : Application
    {
        private Services.LogService? _logger;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化日志服务，供全局使用
            // 在 UI 显示之前初始化，确保能捕获启动过程中的错误
            _logger = new Services.LogService();
            _logger.Log("Application Starting...", Services.LogLevel.Info);

            // 全局异常处理：捕获非 UI 线程抛出的未处理异常
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null)
                {
                    _logger.LogError(ex, "AppDomain.UnhandledException");
                }
                else
                {
                    _logger.Log($"AppDomain.UnhandledException: {args.ExceptionObject}", Services.LogLevel.Error);
                }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                // 捕获主 UI 线程抛出的异常
                _logger.LogError(args.Exception, "DispatcherUnhandledException");
                args.Handled = true; // Prevent crash if possible, or remove this line to let it crash after logging
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                // 捕获未等待 (fire-and-forget) 的 Task 中抛出的异常
                _logger.LogError(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };
        }
    }
}