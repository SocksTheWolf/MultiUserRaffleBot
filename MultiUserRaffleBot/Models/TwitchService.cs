using MultiUserRaffleBot.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace MultiUserRaffleBot.Models
{
    public class TwitchService : BaseService
    {
        private readonly TwitchClient client;
        private readonly TwitchSettings settings;
        private Random rng = new();
        private CancellationTokenSource cancelToken = new();

        // Raffle Data
        private const string WinnerLogFile = "raffle.txt";
        private bool RaffleOpen = false;
        private string CurrentRafflePrize = string.Empty;
        private string CurrentWinnerName = string.Empty;
        private Collection<string> Entries = new();

        public override ConsoleSources GetSource() => ConsoleSources.Twitch;

        public TwitchService(TwitchSettings InSettings)
        {
            settings = InSettings;
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            WebSocketClient customClient = new(clientOptions);

            client = new TwitchClient(customClient)
            {
                AutoReListenOnException = true
            };

#pragma warning disable CS8622
            client.OnJoinedChannel += OnChannelJoined;
            client.OnLeftChannel += OnChannelLeft;
            client.OnChatCommandReceived += OnCommandReceived;
#pragma warning restore CS8622
        }

        protected override bool Internal_Start()
        {
            if (settings.Channels == null)
            {
                PrintMessage("Twitch service is missing channels to connect to!!!");
                return false;
            }

            if (!settings.IsValid())
            {
                PrintMessage("Twitch settings are invalid, cannot continue!");
                return false;
            }

            List<string> ChannelsToConnect = [.. settings.Channels];
            ConnectionCredentials creds = new(settings.BotUserName, settings.OAuthToken);
            client.Initialize(creds, ChannelsToConnect);
            if (client.Connect())
            {
                PrintMessage("Twitch Connected!");
                return true;
            }
            else
            {
                PrintMessage("Twitch could not connect!");
                return false;
            }
        }

        public void JoinChannels(TwitchSettings NewSettings)
        {
            if (!client.IsConnected || !HasStarted)
                return;

            // GetJoinedChannel throws exceptions unless we have channels we've
            // already joined. If we haven't joined any channels, then just join
            // all of them.
            if (client.JoinedChannels.Count < 1)
            {
                PrintMessage($"Attempting to join {NewSettings.Channels.Count()} channels...");
                foreach (string channel in NewSettings.Channels)
                    client.JoinChannel(channel);

                return;
            }

            // Otherwise, if we have already joined channels, only join the ones we haven't
            // joined before.
            foreach (string channel in NewSettings.Channels)
            {
                // Figure out if we haven't joined this channel previously and join it.
                if (client.GetJoinedChannel(channel) == null)
                {
                    PrintMessage($"Attempting to join channel {channel}...");
                    client.JoinChannel(channel);
                }  
            }

            // Reconcile any channels we were in, and part the channel.
            var ChannelsToLeave = client.JoinedChannels.Where((JoinedChannel channel) => { return NewSettings.Channels.Contains(channel.Channel) == false; });
            int NumChannels = ChannelsToLeave.Count();
            if (NumChannels > 0)
            {
                PrintMessage($"There are {NumChannels} twitch channels to leave");
                foreach (JoinedChannel leavingChannel in ChannelsToLeave)
                {
                    PrintMessage($"Attempting to leave channel {leavingChannel.Channel}...");
                    client.LeaveChannel(leavingChannel);
                }
            }
        }

        /*** Raffle Support ***/
        public void StartRaffle(string rafflePrize, int raffleLength)
        {
            // If the raffle prize string is just empty, skip the command
            if (string.IsNullOrWhiteSpace(rafflePrize))
                return;

            RaffleOpen = true;
            Entries.Clear();
            CurrentRafflePrize = rafflePrize;
            SendMessageToAllChannels($"Raffle is now open for {CurrentRafflePrize} for {raffleLength/60} minutes! Type !enter to enter.");
            PrintMessage($"Raffle has now opened for {CurrentRafflePrize}!");
        }

        public void PickRaffle()
        {
            if (!HasStarted)
            {
                PrintMessage("Twitch configuration was invalid, cannot run raffles");
                return;
            }

            // Check to see if a raffle is actually running.
            if (string.IsNullOrEmpty(CurrentRafflePrize))
            {
                PrintMessage("No raffle is currently open!");
                return;
            }

            RaffleOpen = false;
            if (Entries.Count <= 0)
            {
                PrintMessage($"There are no entries for the prize {CurrentRafflePrize} moving forward...");
                SendMessageToAllChannels($"Raffle for prize {CurrentRafflePrize} ended with no claims. Prize may appear again in the future");
                WriteRaffleResult("NO_ENTRIES!");
                Invoke(new SourceEvent(SourceEventType.ReadyToRaffle));
                return;
            }

            // Choose a winner
            int ChooseIndex = rng.Next(Entries.Count);
            CurrentWinnerName = Entries[ChooseIndex].ToLower();

            // Print a message
            PrintMessage($"Winner picked {CurrentWinnerName} at index {ChooseIndex} from {Entries.Count} entries");

            // Remove this selected winner, because if we have to reroll, then this person won't be a potential choice.
            Entries.RemoveAt(ChooseIndex);

            // Open the confirm window for 5 minutes
            SendMessageToAllChannels($"Raffle winner of {CurrentRafflePrize} is @{CurrentWinnerName}! Type !confirm within 5 minutes to confirm!");
            Task.Run(async () =>
            {
                // 300000 is 5 minutes in ms
                await Task.Delay(300000, cancelToken.Token);
                PrintMessage($"Raffle prize for {CurrentRafflePrize} was not claimed, redrawing...");
                PickRaffle();
            }, cancelToken.Token);
        }

        private void WriteRaffleResult(string winner)
        {
            // Print out the winner to a log file.
            using (StreamWriter FileWriter = File.AppendText(WinnerLogFile))
            {
                FileWriter.WriteLine($"{CurrentRafflePrize} winner is {winner}");
            }
            CurrentRafflePrize = string.Empty;
        }

        private void CancelWait()
        {
            cancelToken.Cancel();
            cancelToken.Dispose();
            cancelToken = new();
        }

        /*** Handle Twitch Events ***/
        private void OnChannelJoined(object unused, OnJoinedChannelArgs args)
        {
            PrintMessage($"Joined channel: {args.Channel}");
        }

        private void OnChannelLeft(object unused, OnLeftChannelArgs args)
        {
            PrintMessage($"Left channel: {args.Channel}");
        }

        private void OnCommandReceived(object unused, OnChatCommandReceivedArgs args)
        {
            string loweredCommand = args.Command.CommandText.ToLower();
            string user = args.Command.ChatMessage.Username.ToLower();

            if (loweredCommand == "enter")
            {
                // If raffles are opened and they haven't entered yet,
                // enter the user
                if (RaffleOpen && !Entries.Contains(user))
                {
                    Entries.Add(user);
                    if (settings.RespondToRaffleEntry)
                        SendMessageToChannel(args.Command.ChatMessage.Channel, $"@{user} you have entered!");
                }
            }
            else if ((loweredCommand == "confirm" || loweredCommand == "claim") && !string.IsNullOrEmpty(CurrentWinnerName))
            {
                if (user == CurrentWinnerName)
                {
                    CancelWait();
                    SendMessageToAllChannels($"{CurrentRafflePrize} claimed by @{CurrentWinnerName}! Congrats! {settings.WinnerInstructions}");
                    WriteRaffleResult(CurrentWinnerName);
                    Invoke(new SourceEvent(SourceEventType.ReadyToRaffle));
                }
                else
                {
                    SendMessageToChannel(args.Command.ChatMessage.Channel, $"Sorry, @{user}, it is too late to claim the prize.");
                }
            }
        }

        /*** Sending messages to a channel ***/
        public void SendMessageToChannel(string channel, string message)
        {
            try
            {
                client.SendMessage(channel, message);
            }
            catch (Exception ex)
            {
                PrintMessage($"Encountered exception upon sending message to channel[{channel}]: {ex}");
            }
        }

        public void SendMessageToChannel(JoinedChannel channel, string message)
        {
            try
            {
                client.SendMessage(channel, message);
            }
            catch (Exception ex)
            {
                PrintMessage($"Encountered exception upon sending message to channel[{channel.Channel}]: {ex}");
            }
        }

        public void SendMessageToAllChannels(string message)
        {
            IReadOnlyList<JoinedChannel> AllJoinedChannels = client.JoinedChannels;
            if (AllJoinedChannels.Count <= 0 || string.IsNullOrWhiteSpace(message))
                return;

            foreach (JoinedChannel channel in AllJoinedChannels)
                SendMessageToChannel(channel, message);
        }
    }
}
