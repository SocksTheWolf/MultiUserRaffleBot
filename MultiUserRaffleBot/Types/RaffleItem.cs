using Newtonsoft.Json;

namespace MultiUserRaffleBot.Types
{
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class RaffleItem
    {
        public string Artist { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Amount { get; set; } = 0.0;
    }
}
