﻿using MultiUserRaffleBot.Types;
using System;
using System.Collections.Concurrent;
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
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;

namespace MultiUserRaffleBot.Models
{
    public enum RaffleState
    {
        Waiting,
        RaffleOpen,
        WinnerPicked,
        NoEntries,
        Claimed
    }

    public struct MessageData
    {
        public string Channel;
        public string Message;
        public MessageData(string channel, string message)
        {
            Channel = channel;
            Message = message;
        }

        public bool IsValid() => !string.IsNullOrWhiteSpace(Channel) && !string.IsNullOrWhiteSpace(Message);
    }

    public class TwitchService : BaseServiceTickable
    {
        private readonly TwitchClient client;
        private readonly TwitchSettings settings;
        private Random rng;
        private CancellationTokenSource cancelToken = new();

        // Message Queue
        private ConcurrentQueue<MessageData> MessageQueue = new();

        // Raffle Data
        private bool RaffleOpen = false;
        private const string WinnerLogFile = "raffle.txt";
        private Collection<string> Entries = new();

        // Raffle Strings
        private string CurrentRaffleMessage = string.Empty;
        private string CurrentRafflePrize = string.Empty;
        private string CurrentWinnerName = string.Empty;

        public override ConsoleSources GetSource() => ConsoleSources.Twitch;

        public TwitchService(TwitchSettings InSettings)
        {
            rng = new Random(Guid.NewGuid().GetHashCode());

            settings = InSettings;
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            TcpClient customClient = new TcpClient(clientOptions);
            client = new TwitchClient(customClient)
            {
                AutoReListenOnException = true
            };

#pragma warning disable CS8622
            client.OnJoinedChannel += OnChannelJoined;
            client.OnLeftChannel += OnChannelLeft;
            client.OnChatCommandReceived += OnCommandReceived;
            client.OnError += OnError;
            if (settings.ShouldLog)
                client.OnLog += OnLog;
            client.OnConnected += OnConnected;
            client.OnConnectionError += OnConnectionError;
            client.OnDisconnected += OnDisconnection;
#pragma warning restore CS8622
        }

        protected override bool Internal_Start()
        {
            base.Internal_Start();

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
            CurrentRafflePrize = rafflePrize;
            SetCurrentRaffleMessage(RaffleState.RaffleOpen, raffleLength);

            SendCurrentStatusToAllChannels();
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

                SetCurrentRaffleMessage(RaffleState.NoEntries);
                SendCurrentStatusToAllChannels();

                WriteRaffleResult("NO_ENTRIES!");
                Invoke(new SourceEvent(SourceEventType.ReadyToRaffle));
                return;
            }

            // Choose a winner
            int ChooseIndex = rng.Next(0, Entries.Count);
            CurrentWinnerName = Entries[ChooseIndex].ToLower();

            // Print a message
            PrintMessage($"Winner picked {CurrentWinnerName} at index {ChooseIndex} from {Entries.Count} entries");

            // Remove this selected winner, because if we have to reroll, then this person won't be a potential choice.
            Entries.RemoveAt(ChooseIndex);

            // Open the confirm window for 5 minutes
            SetCurrentRaffleMessage(RaffleState.WinnerPicked);
            SendCurrentStatusToAllChannels();

            Task.Run(async () => {
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
            ResetRaffle();
        }

        private void SetCurrentRaffleMessage(RaffleState state, int raffleLength = 600)
        {
            switch (state)
            {
                case RaffleState.RaffleOpen:
                    CurrentRaffleMessage = $"Drawing is now open for {CurrentRafflePrize} for {raffleLength / 60} minutes! Type !enter to enter.";
                    break;
                case RaffleState.WinnerPicked:
                    CurrentRaffleMessage = $"Winner of {CurrentRafflePrize} is @{CurrentWinnerName}! Type !confirm within 5 minutes to confirm!";
                    break;
                case RaffleState.Claimed:
                    CurrentRaffleMessage = $"{CurrentRafflePrize} claimed by @{CurrentWinnerName}! Congrats! {settings.WinnerInstructions}";
                    break;
                case RaffleState.NoEntries:
                    CurrentRaffleMessage = $"Drawing for prize {CurrentRafflePrize} ended with no claims. Prize may appear again in the future...";
                    break;
                default:
                    CurrentRaffleMessage = string.Empty;
                    break;
            }
        }

        private void CancelWait()
        {
            cancelToken.Cancel();
            cancelToken.Dispose();
            cancelToken = new();
        }

        private void ResetRaffle()
        {
            CurrentRafflePrize = string.Empty;
            CurrentWinnerName = string.Empty;
            Entries.Clear();

            // Reseed RNG
            rng = new Random(Guid.NewGuid().GetHashCode());
        }

        /*** Handle Logging Events ***/
        private void OnLog(object unused, OnLogArgs args)
        {
            PrintMessage($"{args.Data}");
        }

        private void OnError(object unused, OnErrorEventArgs args)
        {
            PrintMessage($"ERROR {args.Exception.Message}");
        }

        /*** Handling Connection Events ***/
        private void OnConnected(object unused, OnConnectedArgs args)
        {
            PrintMessage("Bot Connected");
        }

        private void OnConnectionError(object unused, OnConnectionErrorArgs args)
        {
            PrintMessage($"CONN ERROR {args.Error.Message}");
        }

        private void OnDisconnection(object unused, OnDisconnectedEventArgs args)
        {
            PrintMessage($"Bot Disconnected!!!");
        }

        /*** Handle Twitch Events ***/
        private void OnChannelJoined(object unused, OnJoinedChannelArgs args)
        {
            PrintMessage($"Joined channel: {args.Channel}");
            // Send any status messages to the channel if it was force reconnected.
            SendMessageToChannel(args.Channel, CurrentRaffleMessage);
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
                    PrintMessage($"{user} entered at index {Entries.Count-1}");
                    if (settings.RespondToRaffleEntry)
                        SendMessageToChannel(args.Command.ChatMessage.Channel, $"@{user} you have entered!");
                }
            }
            else if ((loweredCommand == "confirm" || loweredCommand == "claim") && !string.IsNullOrEmpty(CurrentWinnerName))
            {
                if (user == CurrentWinnerName)
                {
                    CancelWait();

                    // Push updates to everyone
                    PrintMessage($"{CurrentWinnerName} has claimed the prize {CurrentRafflePrize}");
                    SetCurrentRaffleMessage(RaffleState.Claimed);
                    WriteRaffleResult(CurrentWinnerName);
                    SendCurrentStatusToAllChannels();

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
            if (string.IsNullOrWhiteSpace(message))
                return;

            MessageQueue.Enqueue(new MessageData(channel, message));
        }

        public void SendMessageToChannel(JoinedChannel channel, string message)
        {
            SendMessageToChannel(channel.Channel, message);
        }

        private void SendMessageToChannel(MessageData msg)
        {
            MessageQueue.Enqueue(msg);
        }

        public void SendMessageToAllChannels(string message)
        {
            IReadOnlyList<JoinedChannel> AllJoinedChannels = client.JoinedChannels;
            if (AllJoinedChannels.Count <= 0 || string.IsNullOrWhiteSpace(message))
                return;

            foreach (JoinedChannel channel in AllJoinedChannels)
                SendMessageToChannel(channel, message);
        }

        private void SendCurrentStatusToAllChannels() => SendMessageToAllChannels(CurrentRaffleMessage);

        /*** Handling sending messages internally ***/
        protected override async Task Tick()
        {
            while (ShouldRun)
            {
                await Task.Delay(1500);

                if (!client.IsConnected) 
                    continue;

                if (!MessageQueue.IsEmpty)
                {
                    if (MessageQueue.TryDequeue(out MessageData msg))
                    {
                        // If message is not valid, just drop the message.
                        if (!msg.IsValid())
                            continue;

                        try
                        {
                            client.SendMessage(msg.Channel, msg.Message);
                        }
                        catch (Exception ex)
                        {
                            PrintMessage($"Encountered exception upon sending message to channel[{msg.Channel}]: {ex}");
                        }
                    }
                }
            }
        }
    }
}
