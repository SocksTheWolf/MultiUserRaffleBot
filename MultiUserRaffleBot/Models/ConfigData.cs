using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using MultiUserRaffleBot.Types;

namespace MultiUserRaffleBot.Models
{
    /*** Base Types/Classes ***/
    using RequiredFieldContainer = List<string>;
    public abstract class SettingsVerifier
    {
        public abstract void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj);
        public bool IsValid()
        {
            RequiredFieldContainer checkIfNotNull = [];
            AddRequiredFields(ref checkIfNotNull);

            if (checkIfNotNull.Any(it => string.IsNullOrEmpty(it)))
                return false;
            return true;
        }
    }

    /*** Settings for Tiltify ***/
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TiltifySettings : SettingsVerifier
    {
        public string ClientID { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string CampaignID { get; set; } = string.Empty;

        // https://github.com/Tiltify/api/issues/9 (it's 5, and I will come after you if you limit me)
        public int PollingInterval { get; set; } = 5;

        public bool Debug { get; set; } = false;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            RequiredFieldObj.AddRange([ClientID, ClientSecret, CampaignID]);
        }
    }

    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TwitchSettings : SettingsVerifier
    {
        public string[] Channels { get; set; } = [];
        public string BotUserName { get; set; } = string.Empty;
        public string OAuthToken { get; set; } = string.Empty;
        public bool RespondToRaffleEntry { get; set; } = false;
        public string WinnerInstructions { get; set; } = string.Empty;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Channels != null && Channels.Length > 0)
                RequiredFieldObj.AddRange(Channels);

            RequiredFieldObj.AddRange([BotUserName, OAuthToken]);
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigData
    {
        // Internals
        public bool IsValid { get; private set; } = false;

        // Statics
        private static readonly string FileName = "config.json";

        /*** Twitch Settings ***/
        [JsonProperty(PropertyName = "twitch")]
        public TwitchSettings TwitchSettings { get; set; } = new TwitchSettings();

        /*** Tiltify Settings ***/
        [JsonProperty(PropertyName = "tiltify")]
        public TiltifySettings TiltifySettings { get; set; } = new TiltifySettings();

        /*** Raffle Settings ***/
        [JsonProperty(Required = Required.Always)]
        public RaffleItem[] RaffleData = [];

        /*** UI Settings ***/
        [JsonProperty]
        public int MaxMessageLifetime = 5;

        /*** Config Loading/Saving ***/
        public static ConfigData? LoadConfigData()
        {
            if (!File.Exists(FileName))
            {
                ConfigData configData = new();
                configData.SaveConfigData(true);
                return configData;
            }

            string json = File.ReadAllText(FileName);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var outputConfig = JsonConvert.DeserializeObject<ConfigData>(json);
                    if (outputConfig != null)
                    {
                        RequiredFieldContainer checkIfNotNull = [];

                        // Get all of our properties in the config class of type SettingsVerifier
                        var verifyProperties = outputConfig.GetType().GetProperties().Where(prop => prop.PropertyType.IsSubclassOf(typeof(SettingsVerifier)));
                        foreach (PropertyInfo? property in verifyProperties)
                        {
                            // Attempt to get the value of the property if it is set
                            object? Value = property?.GetValue(outputConfig);

                            // We have the object, so cast it to the SettingsVerifier class and add the required fields
                            if (Value != null)
                            {
                                ((SettingsVerifier)Value).AddRequiredFields(ref checkIfNotNull);
                            }
                        }

                        // Check if any of the settings are invalid.
                        if (checkIfNotNull.Any(it => string.IsNullOrEmpty(it)))
                            outputConfig.IsValid = false;
                        else
                            outputConfig.IsValid = true;

                        Console.WriteLine("Settings loaded");
                        return outputConfig;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load settings {ex}");
                }
            }
            
            return null;
        }

        public void SaveConfigData(bool OverrideInvalid = false)
        {
            // If we are not valid, do not allow saving, unless override Invalid is true
            if (!IsValid && !OverrideInvalid) 
                return;

            string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            });
            using (StreamWriter FileWriter = File.CreateText(FileName))
            {
                FileWriter.WriteLine(jsonString);
            }
        }
    }
}
