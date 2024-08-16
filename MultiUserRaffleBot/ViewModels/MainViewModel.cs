using MultiUserRaffleBot.Models;
using MultiUserRaffleBot.Types;

namespace MultiUserRaffleBot.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // Main Components
    private ConfigData Config { get; set; }
    public ConsoleService Console { get; set; } = new ConsoleService();
    private RaffleService Raffle { get; set; } = new RaffleService();

    // Services
    private TiltifyService? CharityTracker { get; set; }
    private TwitchService? Twitch { get; set; }

    public MainViewModel()
    {
        // Load all configuration data
        LoadConfigs();

        // Start the console service
        Console.Start();

        /* Tiltify */
#pragma warning disable CS8602 // Possible null reference argument.
        CharityTracker = new TiltifyService(Config.TiltifySettings);
#pragma warning restore CS8602 // Possible null reference argument.
        CharityTracker.OnConsolePrint = (msg) => Console.AddMessage(msg, CharityTracker);
        CharityTracker.OnSourceEvent += (data) =>
        {
            Console.AddMessage($"Hit Milestone! Raised over {data.Amount}{data.Currency}", CharityTracker);
            Raffle.ReachMilestone(data.Amount);
        };
        CharityTracker.OnAuthUpdate = (data) =>
        {
            Config.TiltifySettings.OAuthToken = data.OAuthToken;
            if (!string.IsNullOrWhiteSpace(data.RefreshToken))
                Config.TiltifySettings.RefreshToken = data.RefreshToken;
            Config.SaveConfigData();
            Console.AddMessage("OAuth Data Updated!", CharityTracker);
        };

        /* Twitch */
        Twitch = new TwitchService(Config.TwitchSettings);
        Twitch.OnConsolePrint = (msg) => Console.AddMessage(msg, Twitch);
        Twitch.Start();

        /* Raffle */
        Raffle.OnConsolePrint = (msg) => Console.AddMessage(msg, Raffle);
        Raffle.OnSourceEvent += (data) => {
            if (data.Type == SourceEventType.StartRaffle)
                Twitch?.StartRaffle($"{data.Message} from {data.Name}");
            else
                Twitch?.PickRaffle();
        };

        Config.SaveConfigData();

        if (!Config.IsValid)
            Console.AddMessage("Invalid configuration, please check configs and restart", ConsoleSources.Main);
        else
            Console.AddMessage("Operations Running!", ConsoleSources.Main);

        CharityTracker.Start();
        Raffle.Start();
    }

    // Separated into a different function to allow for reloading of data
    private void LoadConfigs()
    {
        // Attempt to load Config data
        ConfigData? Load = ConfigData.LoadConfigData();
        if (Load == null)
        {
            // If we already have good config data, then don't reload the config data, and continue as normal.
            if (Config != null)
            {
                Console.AddMessage("Detected an error with config.json, dropping changes", ConsoleSources.Main);
                return;
            }

            // Otherwise, give an empty config data object (which will be invalid)
            Config = new ConfigData();
        }
        else
        {
            // If this is not a first time load, print a message that the config did reload.
            if (Config != null)
                Console.AddMessage("Configuration Reloaded", ConsoleSources.Main);

            Config = Load;
        }

        // Set the max message lifetime
        Console.SetMaxMessageLifetime(Config.MaxMessageLifetime);
        Raffle.SetRaffleTime(Config.RaffleLengthInSec);
        Raffle.BuildRaffleData(Config.RaffleData);
    }

    private void ReloadConfig()
    {
        Console.AddMessage("Attempting to reload configuration...", ConsoleSources.Main);
        // Load up our configs again
        LoadConfigs();
        // Join any channels we haven't before
        Twitch?.JoinChannels(Config.TwitchSettings);
    }

    public void OnReloadButton(object msg)
    {
        ReloadConfig();
    }

    public void OnPickWinner(object msg)
    {
        Twitch?.PickRaffle();
    }
}