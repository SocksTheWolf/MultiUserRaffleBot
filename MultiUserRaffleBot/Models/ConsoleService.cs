using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MultiUserRaffleBot.Utils;
using MultiUserRaffleBot.Types;
using Avalonia.Controls;
using System.Linq;
using System.IO;

namespace MultiUserRaffleBot.Models
{
    public class ConsoleMessage(string inMessage, ConsoleSources inSource)
    {
        private DateTime Date { get; set; } = DateTime.Now;
        public ConsoleSources Source { get; set; } = inSource;
        public string Message { get; set; } = inMessage;

        public bool IsExpired(int messageLifetime)
        {
            if (messageLifetime == 0)
                return false;

            TimeSpan diff = DateTime.Now - Date;
            if (Math.Abs(diff.Minutes) > messageLifetime)
                return true;

            return false;
        }
    }

    public class ConsoleService : BaseServiceTickable
    {
        public ObservableCollection<ConsoleMessage> ConsoleMessages { get; private set; }
        public static DataGrid? ConsoleHistory;
        private int MaxMessageLifetime = 5;
        private static string LogFileName = "log.txt";
        private bool LoggingEnabled = false;

        public ConsoleService()
        {
            ConsoleMessages = [];
            if (File.Exists(LogFileName))
                File.Delete(LogFileName);
        }

        public override ConsoleSources GetSource() => ConsoleSources.None;

        public void ApplySettings(int messageLifetime, bool enableLogs)
        {
            MaxMessageLifetime = messageLifetime;
            LoggingEnabled = enableLogs;
        }

        public void AddMessage(string inMessage, ConsoleSources source = ConsoleSources.None)
        {
            // Don't bother adding messages that are blank
            if (string.IsNullOrWhiteSpace(inMessage))
                return;

            PrintToLog(inMessage, source);

            Dispatcher.UIThread.Post(() => {
                ConsoleMessages.Add(new ConsoleMessage(inMessage, source));
                ConsoleHistory?.ScrollIntoView(ConsoleMessages.Last(), null);
            });
        }

        public void AddMessage(string inMessage, BaseService service)
        {
            AddMessage(inMessage, service.GetSource());
        }

        public void ClearAllMessages()
        {
            Dispatcher.UIThread.Post(() => ConsoleMessages.Clear());
        }

        private void PrintToLog(string inMessage, ConsoleSources source)
        {
            if (!LoggingEnabled)
                return;

            // Print out the winner to a log file.
            using (StreamWriter FileWriter = File.AppendText(LogFileName))
            {
                FileWriter.WriteLine($"[{DateTime.Now}]({source}) {inMessage}");
            }
        }

        protected override async Task Tick()
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
            while (ShouldRun)
            {
                ConsoleMessages.RemoveAll(msg => msg.IsExpired(MaxMessageLifetime));
                await timer.WaitForNextTickAsync(default);
            }
        }
    }
}
