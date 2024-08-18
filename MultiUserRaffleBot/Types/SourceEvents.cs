using System;

namespace MultiUserRaffleBot.Types
{
    public enum SourceEventType
    {
        None,
        GoalEvent,
        StartRaffle,
        EndRaffle,
        ReadyToRaffle
    }

    public class SourceEvent
    {
        public readonly SourceEventType Type = SourceEventType.None;
        // Only show up in raffles
        public string Name = string.Empty;
        public string Message = string.Empty;
        public int RaffleLength = 600;

        // Only shows up in GoalEvent reaches
        public double Amount = 0.0;
        public string Currency = string.Empty;

        public SourceEvent(SourceEventType type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return $"SourceEvent[{Enum.GetName(typeof(SourceEventType), Type)}], amount {Amount}{Currency}";
        }
    }

    // A delegate signature of how events should be handled when they are fired.
    public delegate void SourceEventHandler(SourceEvent obj);
}
