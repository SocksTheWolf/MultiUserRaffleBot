﻿using Newtonsoft.Json;

namespace MultiUserRaffleBot.Types
{
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class RaffleItem
    {
        [JsonProperty(Required = Required.Default)]
        public bool Enabled { get; set; } = true;

        public string Artist { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Amount { get; set; } = 0.0;

        [JsonProperty(Required = Required.Default)]
        public int RaffleTime { get; set; } = 600;
    }
}
