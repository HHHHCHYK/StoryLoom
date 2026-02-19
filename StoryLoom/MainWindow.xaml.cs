using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace StoryLoom;

/// <summary>
/// MainWindow.xaml 的交互逻辑。
/// 负责设置 Blazor WebView 环境和依赖注入容器。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 设置 Blazor + WPF 的依赖注入 (DI) 容器
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddWpfBlazorWebView();
        serviceCollection.AddBlazorWebViewDeveloperTools();
        
        // 注册应用程序服务
        // Singleton (单例): 整个应用程序生命周期内只有一个实例 (用于状态管理)
        serviceCollection.AddSingleton<Services.SettingsService>();
        serviceCollection.AddSingleton<Services.LogService>();
        serviceCollection.AddSingleton<Services.ConversationService>();
        
        // HTTP 客户端和 Transient (瞬态) 服务
        serviceCollection.AddHttpClient<Services.LlmClient>();
        serviceCollection.AddTransient<Services.LlmService>();

        // 构建服务提供者，并将其添加到窗口资源中，以便 BlazorWebView 可以找到它
        var serviceProvider = serviceCollection.BuildServiceProvider();
        Resources.Add("Services", serviceProvider);

        // 初始化：加载配置
        var settingsService = serviceProvider.GetRequiredService<Services.SettingsService>();
        settingsService.LoadConfig();

        // 初始化：加载上次的存档
        var conversationService = serviceProvider.GetRequiredService<Services.ConversationService>();
        _ = conversationService.LoadLatestSaveAsync();
    }
}