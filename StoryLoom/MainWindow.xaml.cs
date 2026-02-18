using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace StoryLoom;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddWpfBlazorWebView();
        serviceCollection.AddBlazorWebViewDeveloperTools();
        
        // Register Services
        serviceCollection.AddSingleton<Services.SettingsService>();
        serviceCollection.AddSingleton<Services.LogService>();
        serviceCollection.AddHttpClient();
        serviceCollection.AddTransient<Services.LlmService>();

        Resources.Add("StoryLoom", serviceCollection.BuildServiceProvider());
    }
}