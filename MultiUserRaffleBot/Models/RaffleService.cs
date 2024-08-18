using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MultiUserRaffleBot.Types;

namespace MultiUserRaffleBot.Models
{
    public class RaffleService : BaseServiceTickable
    {
        private ConcurrentQueue<RaffleItem> RaffleQueue = new();
        private Dictionary<double, RaffleItem> RaffleData = new Dictionary<double, RaffleItem>();
        private CancellationTokenSource cancelToken = new();
        private bool CanRaffle = true;

        public override ConsoleSources GetSource() => ConsoleSources.Raffle;

        public void DrawRaffleNow()
        {
            PrintMessage("Force drawing raffle now...");
            // Cancel any task delay waits
            cancelToken.Cancel();
        }

        public void SetCanRaffle(bool state) => CanRaffle = state;

        public void BuildRaffleData(RaffleItem[] items)
        {
            if (items.Length < 0)
                return;

            RaffleData.Clear();
            RaffleData = items.ToDictionary(itm => itm.Amount, itm => itm);
        }

        public void ReachMilestone(double milestone)
        {
            if (RaffleData.ContainsKey(milestone))
            {
                PrintMessage($"Enqueued a raffle for milestone {milestone}");
                RaffleQueue.Enqueue(RaffleData[milestone]);
            }
        }

        protected override async Task Tick()
        {
            PrintMessage("Raffle Service started!");
            while (ShouldRun)
            {
                // Check to see if we have any commands in the queue to run
                if (!RaffleQueue.IsEmpty && CanRaffle)
                {
                    if (RaffleQueue.TryDequeue(out RaffleItem? currentItem))
                    {
                        PrintMessage($"Now raffling off {currentItem.Type} from {currentItem.Artist}");
                        // Start that raffle guuuuuurl
                        Invoke(new SourceEvent(SourceEventType.StartRaffle)
                        {
                            Name = currentItem.Artist,
                            RaffleLength = currentItem.RaffleTime,
                            Message = currentItem.Type
                        });

                        // Wait however long the raffle is supposed to go
                        try
                        {
                            await Task.Delay(currentItem.RaffleTime * 1000, cancelToken.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            PrintMessage("Raffle wait cancelled.");
                        }
                        finally
                        {
                            cancelToken.Dispose();
                            cancelToken = new();
                        }

                        PrintMessage($"Ending raffle for {currentItem.Type} from {currentItem.Artist}");
                        // End the raffle
                        Invoke(new SourceEvent(SourceEventType.EndRaffle)
                        {
                            Name = currentItem.Artist,
                            Message = currentItem.Type
                        });
                        SetCanRaffle(false);
                    }
                }
                await Task.Delay(1000);
            }
        }
    }
}
