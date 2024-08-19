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
        private RaffleItem? currentRaffleItem = null;
        private bool canRaffle = true;


        public void BuildRaffleData(List<RaffleItem> items)
        {
            if (items.Count < 0)
                return;

            RaffleData.Clear();
            RaffleData = items.Where(itm => itm.Enabled).ToDictionary(itm => itm.Amount, itm => itm);
            PrintMessage($"{RaffleData.Count} items have been added to raffle item dictionary");
        }

        public override ConsoleSources GetSource() => ConsoleSources.Raffle;
        public RaffleItem? GetRaffleItem() => currentRaffleItem;

        public void SetCanRaffle(bool state)
        {
            // Clear out the current raffle item
            if (state == true)
                currentRaffleItem = null;

            canRaffle = state;
        }

        public void DrawRaffleNow()
        {
            if (currentRaffleItem != null)
            {
                PrintMessage("Force drawing raffle now...");
                // Cancel any task delay waits
                cancelToken.Cancel();
            }
            else
                PrintMessage("No raffle is currently running!");
        }        

        public void ReachMilestone(double milestone)
        {
            if (RaffleData.ContainsKey(milestone))
            {
                RaffleItem currentItem = RaffleData[milestone];
                if (currentItem.Enabled)
                {
                    PrintMessage($"Enqueued a raffle for milestone {milestone}");
                    RaffleQueue.Enqueue(currentItem);
                }
            }
        }

        protected override async Task Tick()
        {
            PrintMessage("Raffle Service started!");
            while (ShouldRun)
            {
                // Check to see if we have any commands in the queue to run
                if (!RaffleQueue.IsEmpty && canRaffle)
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

                        currentRaffleItem = currentItem;

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
