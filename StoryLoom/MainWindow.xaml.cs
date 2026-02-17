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

        serviceCollection.AddBlazorWebView();
        serviceCollection.AddBlazorWebViewDeveloperTools();
        Resources.Add("StoryLoom", serviceCollection.BuildServiceProvider());
    }
}