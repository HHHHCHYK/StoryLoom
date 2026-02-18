using System;
using System.Threading.Tasks;
using System.Windows;

namespace StoryLoom
{
    // 这里肯定被 Rider 错改成了 public partial class MainUI : Application
    // 请把它改回 App！
    public partial class App : Application
    {
        private Services.LogService? _logger;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize Logger for global usage
            _logger = new Services.LogService();
            _logger.Log("Application Starting...", Services.LogLevel.Info);

            // Global Exception Handling
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
                _logger.LogError(args.Exception, "DispatcherUnhandledException");
                args.Handled = true; // Prevent crash if possible, or remove this line to let it crash after logging
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                _logger.LogError(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };
        }
    }
}