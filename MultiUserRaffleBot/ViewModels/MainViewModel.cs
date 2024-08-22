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
    private TiltifyService CharityTracker { get; set; }
    private TwitchService Twitch { get; set; }

    public MainViewModel()
    {
        // Start the console service
        Console.Start();

        // Push the raffle handling immediately
        Raffle.OnConsolePrint = (msg) => Console.AddMessage(msg, Raffle);

        // Load all configuration data
        LoadConfigs();

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

        /* Twitch */
        Twitch = new TwitchService(Config.TwitchSettings);
        Twitch.OnConsolePrint = (msg) => Console.AddMessage(msg, Twitch);
        Twitch.OnSourceEvent += (data) => {
            // When Twitch is done running said raffle (meaning something claimed or we ran out of entries)
            // then allow the raffle system to present another raffle entry
            RaffleItem? lastRaffle = Raffle.GetRaffleItem();
            if (lastRaffle != null)
            {
                // Mark this raffle item as complete so we don't end up running it again upon next startup.
                Config.MarkRaffleComplete(lastRaffle);
                Config.SaveConfigData();
            }

            Raffle.SetCanRaffle(true);
        };
        Twitch.Start();

        /* Raffle */
        Raffle.OnSourceEvent += (data) => {
            if (data.Type == SourceEventType.StartRaffle)
                Twitch.StartRaffle($"{data.Message} from {data.Name}", data.RaffleLength);
            else
                Twitch.PickRaffle();
        };

        Config.SaveConfigData();

        if (!Config.IsValid)
            Console.AddMessage("Invalid configuration, please check configs and restart", ConsoleSources.Main);
        else
            Console.AddMessage("Initalization Complete!", ConsoleSources.Main);

        CharityTracker.Start();

        if (CharityTracker.HasStarted && Twitch.HasStarted)
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
        Console.ApplySettings(Config.MaxMessageLifetime, Config.EnableLogging);
        Raffle.BuildRaffleData(Config.RaffleData);
    }

    private void ReloadConfig()
    {
        Console.AddMessage("Attempting to reload configuration...", ConsoleSources.Main);
        // Load up our configs again
        LoadConfigs();
        // Join any channels we haven't before
        Twitch.JoinChannels(Config.TwitchSettings);
    }

    public void OnReloadButton(object msg)
    {
        if (!Twitch.HasStarted || !CharityTracker.HasStarted)
        {
            Console.AddMessage("Configuration cannot be reloaded, requires restart!", ConsoleSources.Main);
            return;
        }

        ReloadConfig();
    }

    public void OnPickWinner(object msg)
    {
        if (!Raffle.HasStarted)
        {
            Console.AddMessage("Raffle service cannot force pick winner as configs are invalid!", ConsoleSources.Main);
            return;
        }
        Raffle.DrawRaffleNow();
    }
}