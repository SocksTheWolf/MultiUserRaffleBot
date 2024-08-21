using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using MultiUserRaffleBot.ViewModels;
using MultiUserRaffleBot.Views;

namespace MultiUserRaffleBot;

public partial class App : Application
{
    private MainWindow? mainWindow;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Handle minimizing to tray upon hitting the minimize state.
    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is MainWindow && e.NewValue is WindowState windowState && windowState == WindowState.Minimized)
        {
            HideApplication();
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            desktop.MainWindow = mainWindow;
            mainWindow.PropertyChanged += MainWindow_PropertyChanged;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void RestoreApplication()
    {
        if (mainWindow != null)
        {
            if (mainWindow.WindowState == WindowState.Normal)
                return;

            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Show();
        }
    }

    public void HideApplication()
    {
        if (mainWindow != null)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Hide();
        }
    }

    public void ToggleState()
    {
        if (mainWindow?.WindowState == WindowState.Normal)
        {
            HideApplication();
        }
        else
        {
            RestoreApplication();
        }
    }
}
