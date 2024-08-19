using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MultiUserRaffleBot.ViewModels
{
    public class ApplicationViewModel : ViewModelBase
    {
        public void ExitCommand(object msg)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        }

        public void RestoreApplication(object msg)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                ((App)Application.Current).RestoreApplication();
            }
        }

        public void ToggleApplication(object msg)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                ((App)Application.Current).ToggleState();
            }
        }
    }
}
