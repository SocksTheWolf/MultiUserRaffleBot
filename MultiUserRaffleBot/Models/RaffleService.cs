using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MultiUserRaffleBot.Types;

namespace MultiUserRaffleBot.Models
{
    public class RaffleService : BaseServiceTickable
    {
        private ConcurrentQueue<RaffleItem> RaffleQueue = new();
        private Dictionary<double, RaffleItem> RaffleData = new Dictionary<double, RaffleItem>();
        private int RaffleLength = 0;

        public override ConsoleSources GetSource() => ConsoleSources.Raffle;

        public void SetRaffleTime(int seconds)
        {
            RaffleLength = seconds * 1000;
        }

        public void BuildRaffleData(RaffleItem[] items)
        {
            if (items.Length > 0)
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
            else
            {
                PrintMessage($"Unable to enqueue a raffle for milestone {milestone}");
            }
        }

        protected override async Task Tick()
        {
            PrintMessage("Raffle Service started!");
            while (ShouldRun)
            {
                // Check to see if we have any commands in the queue to run
                if (!RaffleQueue.IsEmpty)
                {
                    if (RaffleQueue.TryDequeue(out RaffleItem? currentItem))
                    {
                        PrintMessage($"Now raffling off {currentItem.Type} from {currentItem.Artist}");
                        // Start that raffle guuuuuurl
                        Invoke(new SourceEvent(SourceEventType.StartRaffle)
                        {
                            Name = currentItem.Artist,
                            Message = currentItem.Type
                        });

                        // TODO: CONSIDER WARNING MESSAGES???????

                        // Wait however long the raffle is supposed to go
                        await Task.Delay(RaffleLength);

                        PrintMessage($"Ending raffle for {currentItem.Type} from {currentItem.Artist}");
                        // End the raffle
                        Invoke(new SourceEvent(SourceEventType.EndRaffle)
                        {
                            Name = currentItem.Artist,
                            Message = currentItem.Type
                        });
                    }
                }
                await Task.Delay(1000);
            }
        }
    }
}
