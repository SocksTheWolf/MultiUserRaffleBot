using Avalonia.Controls;
using MultiUserRaffleBot.Models;

namespace MultiUserRaffleBot.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // Technically, this runs on the UI thread anyways, so we don't break MVVM :)
        // This thing is annoying and I hate it.
        ConsoleService.ConsoleHistory = ConsoleLog;
    }
}
